using DbfProcessor.Core.Exceptions;
using DbfProcessor.Core.Storage;
using DbfProcessor.Models;
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
        private static DirectoryInfo _exchangeDir;
        private string _currentPackage;
        private readonly Extract _extract;
        private readonly Interaction _interaction;
        private readonly ICollection<Extraction> _extractionModels;
        #endregion
        #region private properties
        private static Logging Log => Logging.GetLogging();
        private static Config Config => ConfigInstance.GetInstance().Config;
        private static Impersonation Impersonation => Impersonation.GetInstance();
        #endregion
        public Exchange()
        {
            _exchangeDir = new DirectoryInfo(Config.ExchangeDirectory);
            _extract = new Extract();
            _extractionModels = new List<Extraction>();
            _interaction = new Interaction();
        }

        public void Run()
        {
            _interaction.ApplyBase();
            ProcessOrphans();
            Partition partition = new Partition(_exchangeDir.GetFiles("*.zip"));
            while (partition.HasNext)
            {
                ProcessPackages(partition.Get());
                _interaction.Process(_extract.GetParents());
                _extract.ClearParents();
            }
            _interaction.ApplyStage();
        }
        #region private
        private void ProcessPackages(FileInfo[] parts)
        {
            foreach (FileInfo file in parts)
            {
                _currentPackage = Path.Combine(Config.DbfLookUpDir, $"{file.Name.Replace(".zip", string.Empty)}");
                if (!Directory.Exists(_currentPackage))
                    ZipFile.ExtractToDirectory(Path.Combine(Config.ExchangeDirectory, file.Name), _currentPackage);
                
                PrepareDbfs();
                FillExtractionModels();
                _extract.Process(_extractionModels);
                TideUp();
            }
        }

        private void TideUp()
        {
            _extractionModels.Clear();
            Directory.Delete(_currentPackage, true);
            foreach (var file in GetDbfs(Config.DbfLookUpDir))
                File.Delete(file.FullName);
        }

        private void ProcessOrphans()
        {
            if (_exchangeDir.GetFiles("*.dbf").Length == 0)
            {
                Log.Accept(new Execution($"There are no any orphans dbfs in {Config.ExchangeDirectory}", 
                    LoggingType.Info));
                return;
            }
            string date = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
            string orphansDirPath = Path.Combine(_exchangeDir.FullName, $"{date}_orphans"); 

            Directory.CreateDirectory(orphansDirPath);
            foreach (FileInfo file in GetDbfs(_exchangeDir.FullName))
            {
                File.Copy(file.FullName, Path.Combine(orphansDirPath, file.Name), true);
                File.Delete(file.FullName);
            }
            ZipFile.CreateFromDirectory(orphansDirPath, $"{orphansDirPath}.zip");
            Directory.Delete(orphansDirPath, true);
        }

        private void PrepareDbfs()
        {
            foreach (FileInfo dbfFile in GetDbfs(_currentPackage))
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                if (dbfFileName.Length > 8)
                {
                    dbfFileName = dbfFileName.Substring(0, dbfFileName.LastIndexOf("_"));
                    string newTrunFilePath = Path.Combine(Config.DbfLookUpDir, $"{dbfFileName}.dbf");
                    File.Move(dbfFile.FullName, newTrunFilePath);
                } 
                else
                {
                    File.Move(dbfFile.FullName, Path.Combine(Config.DbfLookUpDir, dbfFile.Name));
                }
            }
        }

        private void FillExtractionModels()
        {
            foreach (FileInfo dbfFile in GetDbfs(Config.DbfLookUpDir))
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                string tableType = RetrieveTypeFromName(dbfFileName);
                if (NeedSync(dbfFile.Name, tableType))
                {
                    _extractionModels.Add(new Extraction
                    {
                        DbfName = dbfFile.Name,
                        TableName = dbfFileName,
                        Package = _currentPackage,
                        TableType = tableType
                    });
                }
                else
                {
                    Log.Accept(new Execution($"No need to sync [{dbfFile.Name}] of pack: [{_currentPackage}], " +
                        $"it has already handled or ignored",
                        LoggingType.Info));
                }
            }
        }

        private bool NeedSync(string dbf, string type)
        {
            TableInfo info = Impersonation.Get(type);
            if (info.Ignore) return false;
            if (_interaction.GetSyncInfo(dbf, _currentPackage)) return false;
            else return true;
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
