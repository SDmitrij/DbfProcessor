﻿using DbfProcessor.Core.Exceptions;
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
        private readonly Logging _log;
        private readonly ConfigModel _config;
        private readonly Impersonation _impersonation;
        private readonly Extract _extract;
        private readonly Interaction _interaction;
        private readonly ICollection<ExtractionModel> _extractionModels = new List<ExtractionModel>();
        #endregion

        public Exchange(Logging logging, Config config, Impersonation impersonation, Extract extract, Interaction interaction)
        {
            _log = logging;
            _config = config.Get();
            _impersonation = impersonation;
            _extract = extract;
            _interaction = interaction;
            _exchangeDir = new DirectoryInfo(_config.ExchangeDirectory);
        }

        public void Begin()
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
                string currentPackage = Path.Combine(_config.DbfLookUpDir, $"{file.Name.Replace(".zip", string.Empty)}");
                if (!Directory.Exists(currentPackage))
                    ZipFile.ExtractToDirectory(Path.Combine(_config.ExchangeDirectory, file.Name), currentPackage);
                
                PrepareDbfs(currentPackage);
                FillExtractionModels(currentPackage);
                _extract.Process(_extractionModels);
                TideUp(currentPackage);
            }
        }

        private void TideUp(string currentPackage)
        {
            _extractionModels.Clear();
            Directory.Delete(currentPackage, true);
            foreach (var file in GetDbfs(_config.DbfLookUpDir))
                File.Delete(file.FullName);
        }

        private void ProcessOrphans()
        {
            if (_exchangeDir.GetFiles("*.dbf").Length == 0)
            {
                _log.Accept(new Execution($"There are no any orphans dbfs in {_config.ExchangeDirectory}", 
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

        private void PrepareDbfs(string currentPackage)
        {
            var dbfs = GetDbfs(currentPackage);
            foreach (FileInfo dbfFile in dbfs)
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                if (dbfFileName.Length > 8)
                {
                    dbfFileName = dbfFileName.Substring(0, dbfFileName.LastIndexOf("_"));
                    string newTrunFilePath = Path.Combine(_config.DbfLookUpDir, $"{dbfFileName}.dbf");
                    File.Move(dbfFile.FullName, newTrunFilePath);
                } 
                else
                {
                    File.Move(dbfFile.FullName, Path.Combine(_config.DbfLookUpDir, dbfFile.Name));
                }
            }
        }

        private void FillExtractionModels(string currentPackage)
        {
            var dbfs = GetDbfs(_config.DbfLookUpDir);
            foreach (FileInfo dbfFile in dbfs)
            {
                string dbfFileName = dbfFile.Name.Replace(".dbf", string.Empty);
                string tableType = RetrieveTypeFromName(dbfFileName);
                if (NeedSync(dbfFile.Name, tableType, currentPackage))
                {
                    _extractionModels.Add(new ExtractionModel
                    {
                        DbfName = dbfFile.Name,
                        TableName = dbfFileName,
                        Package = currentPackage,
                        TableType = tableType
                    });
                }
                else
                {
                    _log.Accept(new Execution($"No need to sync [{dbfFile.Name}] of pack: [{currentPackage}], " +
                        $"it has already handled or ignored",
                        LoggingType.Info));
                }
            }
        }

        private bool NeedSync(string dbf, string type, string currentPackage)
        {
            TableInfo info = _impersonation.Get(type);
            if (info.Ignore) return false;
            if (_interaction.GetSyncInfo(dbf, currentPackage)) return false;
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
            throw new InfrastructureException($"Empty table type on [{table}]");
        }

        private static FileInfo[] GetDbfs(string path)
        {
            DirectoryInfo dbfDir = new DirectoryInfo(path);
            FileInfo[] dbfFiles = dbfDir.GetFiles("*.dbf");
            if (dbfFiles.Length == 0) 
                throw new InfrastructureException($"Can't get dbfs from dir: {path}");
            return dbfFiles;
        }
        #endregion
    }
}
