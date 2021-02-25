using DbfProcessor.Core.Exceptions;
using DbfProcessor.Core.Storage;
using DbfProcessor.Models;
using DbfProcessor.Models.Dtos;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DbfProcessor.Core
{
    public class Exchange
    {
        #region private fields
        private readonly Extract _extract;
        private readonly Interaction _interaction;
        private readonly ICollection<ExtractionDto> _extractionDtos;
        #endregion
        #region private properties
        private static Logging Log => Logging.GetLogging();
        private static Config Config => ConfigInstance.GetInstance().Config();
        private static DirectoryInfo ExchangeDirInfo => new DirectoryInfo(Config.ExchangeDirectory);
        private Extract Extract => _extract;
        private Interaction Interaction => _interaction;
        private Impersonation Impersonation => Impersonation.GetInstance();
        private ICollection<ExtractionDto> Dtos => _extractionDtos;
        #endregion
        public Exchange()
        {
            _extract = new Extract();
            _extractionDtos = new List<ExtractionDto>();
            _interaction = new Interaction();
        }

        public void Run()
        {
            try
            {
                CheckInfrastructure();
                Interaction.ApplyBase();
                ProcessOrphans();

                Partition partition = new Partition(ExchangeDirInfo.GetFiles("*.zip"));
                while (partition.HasNext)
                {
                    ProcessZips(partition.Get());
                    Interaction.Take(Extract.GetParents());
                    Extract.Clear();
                }

                Interaction.CreateProcedures();
                Interaction.Stage();
            }
            catch (ExchangeException e)
            {
                Log.Accept(new Execution(e.Message));
                throw;
            }
        }
        #region private
        private void CheckInfrastructure()
        {
            if (!Directory.Exists(Config.ExchangeDirectory))
                throw new ExchangeException("Directory that keeps dbfs from exchange is not exists");

            if (!Directory.Exists(Config.DbfLookUpDir))
                throw new ExchangeException("Directory that handle dbfs is not exists");

            if (ExchangeDirInfo.GetFiles("*.zip").Length == 0 
                && ExchangeDirInfo.GetFiles("*.dbf").Length == 0)
                throw new ExchangeException("Exchange directory is empty");
        }

        private void ProcessZips(FileInfo[] parts)
        {
            foreach (FileInfo file in parts)
            {
                string currUnzip = Path.Combine(Config.ExchangeDirectory,
                    $"{file.Name.Replace(".zip", string.Empty)}");

                if (!Directory.Exists(currUnzip))
                    ZipFile.ExtractToDirectory(Path.Combine(Config.ExchangeDirectory, file.Name), currUnzip);

                DirectoryInfo currUnzipDirInfo = new DirectoryInfo(currUnzip);
                string newUnzipPath = Path.Combine(Config.DbfLookUpDir, currUnzipDirInfo.Name);
                currUnzipDirInfo.MoveTo(newUnzipPath);
                CopyToLookUp(currUnzipDirInfo.FullName);
                FillExtractionDtos(currUnzipDirInfo.FullName);
                Extract.Process(Dtos);
                Dtos.Clear();
                Directory.Delete(newUnzipPath, true);
                CleanLookUp();
            }
        }

        private void ProcessOrphans()
        {
            if (ExchangeDirInfo.GetFiles("*.dbf").Length == 0)
            {
                Log.Accept(new Execution($"There are no any orphans dbfs in {Config.ExchangeDirectory}", 
                    LoggingType.Info));
                return;
            }
            string date = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
            string orphansDirPath = Path.Combine(ExchangeDirInfo.FullName, $"{date}_orphans"); 

            Directory.CreateDirectory(orphansDirPath);
            foreach (FileInfo file in GetDbfs(ExchangeDirInfo.FullName))
            {
                File.Copy(file.FullName, Path.Combine(orphansDirPath, file.Name), true);
                File.Delete(file.FullName);
            }
            ZipFile.CreateFromDirectory(orphansDirPath, $"{orphansDirPath}.zip");
            Directory.Delete(orphansDirPath, true);
        }

        private void CopyToLookUp(string package)
        {
            foreach (FileInfo dbfFile in GetDbfs(package))
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                if (dbfFileName.Length > 8)
                {
                    dbfFileName = dbfFileName.Substring(0, dbfFileName.LastIndexOf("_"));
                    string newTrunFilePath = Path.Combine(Config.DbfLookUpDir, $"{dbfFileName}.dbf");
                    File.Move(dbfFile.FullName, newTrunFilePath);
                } else
                {
                    File.Move(dbfFile.FullName, Path.Combine(Config.DbfLookUpDir, dbfFile.Name));
                }
            }
        }

        private void FillExtractionDtos(string package)
        {
            foreach (FileInfo dbfFile in GetDbfs(Config.DbfLookUpDir))
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                string tableType = RetrieveTypeFromName(dbfFileName);
                if (NeedSync(dbfFile.Name, Impersonation.GetImpersonateTable(tableType)))
                {
                    Dtos.Add(new ExtractionDto
                    {
                        DbfName = dbfFile.Name,
                        TableName = dbfFileName,
                        Package = package,
                        TableType = tableType
                    });
                }
                else
                {
                    Log.Accept(new Execution($"No need to sync {dbfFile.Name}, " +
                        $"it has already handled or ignored",
                        LoggingType.Info));
                }
            }
        }

        private bool NeedSync(string dbf, TableInfo info)
        {
            if (info.Ignore) return false;
            if (Interaction.GetSyncInfo(dbf))
                return false;
            else
                return true;
        }

        private static string RetrieveTypeFromName(string table)
        {
            if (Regex.IsMatch(table, @"^\d*_"))
            {
                int typeLen = table.Length - table.IndexOf("_");
                return table.Substring(table.IndexOf("_") + 1, typeLen - 1);
            }
            if (Regex.IsMatch(table, @"^\S*_")) return table.Substring(0, table.IndexOf("_"));
            throw new ExchangeException($"Empty table type on [{table}]");
        }

        private static void CleanLookUp()
        {
            foreach (var file in GetDbfs(Config.DbfLookUpDir))
                File.Delete(file.FullName);
        }

        private static FileInfo[] GetDbfs(string path)
        {
            DirectoryInfo dbfDir = new DirectoryInfo(path);
            FileInfo[] dbfFiles = dbfDir.GetFiles("*.dbf");
            if (dbfFiles.Length == 0) 
                throw new ExchangeException($"Can't get dbfs from dir: {path}");
            return dbfFiles;
        }
        #endregion
    }
}
