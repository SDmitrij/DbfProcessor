using DbfProcessor.Core.Storage;
using DbfProcessor.Models;
using DbfProcessor.Models.Infrastructure;
using DbfProcessor.Out;
using DbfProcessor.Out.Concrete;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace DbfProcessor.Core
{
    public class Exchange
    {
        #region private fields
        private readonly Extract _extract = new Extract();
        #endregion
        #region private properties
        private static Logging Log => Logging.GetLogging();
        private static Config Config => ConfigInstance.GetInstance().Config();
        private static DirectoryInfo ExchangeDirInfo => new DirectoryInfo(Config.ExchangeDirectory);
        public ICollection<SharedParent> GetResults() => _extract.GetParents();
        private Extract Extract => _extract;
        private Interaction Interaction => new Interaction();
        #endregion
        public void Run()
        {
            try
            {
                CheckInfrastructure();
                Interaction.ApplyBase();
                HandleOrphans();
                HandleZip();
                Interaction.Stage();
            } catch (Exception e)
            {
                Log.Accept(new Execution(e.Message));
            }
        }
        #region private
        private void CheckInfrastructure()
        {
            if (!Directory.Exists(Config.ExchangeDirectory))
                throw new Exception("Directory that keeps dbfs from exchange is not exists");

            if (!Directory.Exists(Config.DbfLookUpDir))
                throw new Exception("Directory that handle dbfs is not exists");

            if (ExchangeDirInfo.GetFiles("*.zip").Length == 0 
                && ExchangeDirInfo.GetFiles("*.dbf").Length == 0)
                throw new Exception("Exchange directory is empty");
        }

        private void HandleZip()
        {
            Partition partition = new Partition(ExchangeDirInfo.GetFiles("*.zip"));
            while (partition.HasNext)
            {
                FileInfo[] partitions = partition.Get();
                foreach (FileInfo file in partitions)
                {
                    string currUnzip = Path.Combine(Config.ExchangeDirectory,
                        $"{file.Name.Replace(".zip", string.Empty)}");

                    if (!Directory.Exists(currUnzip))
                        ZipFile.ExtractToDirectory(Path.Combine(Config.ExchangeDirectory, file.Name), currUnzip);

                    DirectoryInfo currUnzipDirInfo = new DirectoryInfo(currUnzip);
                    string newUnzipPath = Path.Combine(Config.DbfLookUpDir, currUnzipDirInfo.Name);
                    currUnzipDirInfo.MoveTo(newUnzipPath);
                    Extraction(currUnzipDirInfo.FullName);
                    Directory.Delete(newUnzipPath, true);
                }
                Interaction.Take(Extract.GetParents());
                Extract.Clear();
            }
        }

        private void HandleOrphans()
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

        private void Extraction(string package)
        {
            foreach (FileInfo dbfFile in GetDbfs(package))
            {
                string lookedUpDbf = Path.Combine(Config.DbfLookUpDir, dbfFile.Name); 
                File.Move(dbfFile.FullName, lookedUpDbf);
                Extract.ProcessDbf(package);
                File.Delete(lookedUpDbf);
            }
        }

        private static FileInfo[] GetDbfs(string handlingPath)
        {
            DirectoryInfo dbfDir = new DirectoryInfo(handlingPath);
            FileInfo[] dbfFiles = dbfDir.GetFiles("*.dbf");
            if (dbfFiles.Length == 0) 
                throw new Exception($"Can't get dbfs from dir: {handlingPath}");
            return dbfFiles;
        }
        #endregion
    }
}
