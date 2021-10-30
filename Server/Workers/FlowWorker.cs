using System.Text.RegularExpressions;
using FileFlow.Server.Controllers;
using FileFlow.Server.Helpers;
using FileFlow.Shared;
using FileFlow.Shared.Models;

namespace FileFlow.Server.Workers
{
    public class FlowWorker : Worker
    {
        public bool Processing { get; private set; }
        public string CurrentFile { get; private set; }
        private Library Library { get; set; }

        public FlowWorker() : base(ScheduleType.Minute, 1) { }


        protected override void Execute()
        {
            if (string.IsNullOrEmpty(CurrentFile))
            {
                Logger.Instance.DLog("Nothing to process");
                return;
            }
            FileInfo file = new FileInfo(CurrentFile);
            var flow = new FlowController().Get(Library.Flow);
            if (file.Exists == false || flow == null)
            {
                if (flow == null)
                    Logger.Instance.WLog("Flow not found: " + Library.Flow);
                else
                    Logger.Instance.WLog("File not found: " + CurrentFile);
                lock (this)
                {
                    Processing = false;
                    CurrentFile = "";
                    Library = null;
                }
            }

            var libraryFile = DbHelper.Single<LibraryFile>("name = @1", file.FullName);
            if (libraryFile.Uid == Guid.Empty)
            {
                libraryFile = DbHelper.Update(new LibraryFile
                {
                    Name = file.FullName,
                    FlowName = flow.Name,
                    DateCreated = file.CreationTime,
                    DateModified = file.LastWriteTime
                });
            }
            else
            {
                // shoudl do check here if its been changed???
                libraryFile.FlowName = flow.Name;
                libraryFile.DateCreated = file.CreationTime;
                libraryFile.DateModified = file.LastWriteTime;
                DbHelper.Update(libraryFile);
            }
            Logger.Instance.ILog("############################# PROCESSING:  " + file.FullName);
            var executor = new FlowExecutor();
            executor.Logger = new FlowLogger
            {
                File = libraryFile
            };
            executor.Flow = flow;
            executor.Run(file.FullName);
        }

        public bool Process(Library library, string file)
        {
            lock (this)
            {
                if (this.Processing || library == null || library.Flow == Guid.Empty)
                    return false;
                this.CurrentFile = file;
                this.Library = library;
                this.Processing = true;
            }
            this.Trigger();
            return true;
        }
    }
}