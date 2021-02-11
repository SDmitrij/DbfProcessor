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
        #region private
        private Logging Log => Logging.GetLogging();
        private Config Config => ConfigInstance.GetInstance().Config();
        private DirectoryInfo ExchangeDirInfo => new DirectoryInfo(Config.ExchangeDirectory);
        public ICollection<SharedParent> GetResults() => _extract.GetTables();
       
        private readonly Extract _extract = new Extract();
        private Extract Extract => _extract;
        #endregion
        public void Run()
        {
            try
            {
                CheckInfrastructure();
                HandleOrphans();
                HandleZip();
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
            foreach (FileInfo file in ExchangeDirInfo.GetFiles("*.zip"))
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
                file.Delete();
            }
            ZipFile.CreateFromDirectory(orphansDirPath, $"{orphansDirPath}.zip");
            Directory.Delete(orphansDirPath, true);
        }

        private void Extraction(string path)
        {
            foreach (FileInfo dbfFile in GetDbfs(path))
            {
                string lookedUpDbf = Path.Combine(Config.DbfLookUpDir, dbfFile.Name); 
                File.Move(dbfFile.FullName, lookedUpDbf);
                Extract.ProcessDbf(path);
                File.Delete(lookedUpDbf);
            }
        }

        private FileInfo[] GetDbfs(string handlingPath)
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
