using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ShareX;
using ShareX.HelpersLib;
using ShareX.UploadersLib;

static class UploadConsentChecks
{
    const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static object? Invoke(WorkerTask task, string method, params object[] args)
    {
        try { return typeof(WorkerTask).GetMethod(method, Instance)!.Invoke(task, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    sealed class MemoryService(FileDestination destination, bool fail) : FileUploaderService
    {
        public override FileDestination EnumValue => destination;
        public int ConfigChecks, Created, Uploads;
        public override bool CheckConfig(UploadersConfig config) { ConfigChecks++; return true; }
        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        { Created++; return new MemoryUploader(this, fail); }
        sealed class MemoryUploader(MemoryService owner, bool fail) : GenericUploader
        {
            public override UploadResult Upload(Stream stream, string fileName)
            {
                owner.Uploads++;
                var result = new UploadResult { URL = fail ? null : "https://synthetic.invalid/result" };
                if (fail) result.Errors.Add("Synthetic failure; no network request was made.");
                return result;
            }
        }
    }

    public static void Run(string root, Action<bool, string> check)
    {
        Type program = typeof(TaskSettings).Assembly.GetType("ShareX.Program", true)!;
        var properties = new[] { "Settings", "DefaultTaskSettings", "UploadersConfig" }.ToDictionary(x => x, x => program.GetProperty(x, Static)!);
        var previous = properties.ToDictionary(x => x.Key, x => x.Value.GetValue(null));
        var config = new ApplicationConfig { UseSecondaryUploaders = true };
        properties["Settings"].SetValue(null, config);
        properties["DefaultTaskSettings"].SetValue(null, config.DefaultTaskSettings);
        properties["UploadersConfig"].SetValue(null, new UploadersConfig());
        FileDestination primaryKey = FileDestination.CustomFileUploader, secondaryKey = FileDestination.SharedFolder;
        var originalPrimary = UploaderFactory.FileUploaderServices[primaryKey];
        var originalSecondary = UploaderFactory.FileUploaderServices[secondaryKey];
        var primary = new MemoryService(primaryKey, true);
        var secondary = new MemoryService(secondaryKey, false);
        try
        {
            var filtered = new TaskSettings { AfterCaptureJob = AfterCaptureTasks.None, AfterUploadJob = AfterUploadTasks.None };
            filtered.UploadSettings.UploaderFilters = [new UploaderFilter(originalSecondary.ServiceIdentifier, "png")];
            using (var task = WorkerTask.CreateDataUploaderTask(EDataType.Image, new MemoryStream([1, 2, 3]), "synthetic.png", filtered))
            {
                int calls = 0;
                IGenericUploaderService? selected = null; string? selectedName = null;
                task.UploadConfirmation = (service, name) => { calls++; selected = service; selectedName = name; return false; };
                Invoke(task, "CreateTaskReferenceHelper");
                bool error = (bool)Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0)!;
                check(selected?.ServiceIdentifier == originalSecondary.ServiceIdentifier && selectedName == "synthetic.png", "consent sees extension-filter destination instead of default Imgur");
                check(calls == 1 && task.StopRequested && !error && !task.Info.Result.IsURLExpected, "filtered upload cancellation is a stop, not a retryable upload error");
            }

            string image = Path.Combine(root, "synthetic.bmp");
            using (var bitmap = new Bitmap(24, 24)) bitmap.Save(image, ImageFormat.Bmp);
            var processed = new TaskSettings { AfterCaptureJob = AfterCaptureTasks.UploadImageToHost, AfterUploadJob = AfterUploadTasks.None };
            processed.AdvancedSettings.ProcessImagesDuringFileUpload = true;
            processed.ImageSettings.ImageFormat = EImageFormat.PNG;
            processed.UploadSettings.UploaderFilters = [new UploaderFilter(originalSecondary.ServiceIdentifier, "png")];
            using (var task = WorkerTask.CreateFileUploaderTask(image, processed))
            {
                check(task.Info.Job == TaskJob.Job && task.Image != null, "processed file follows real image-processing path");
                Invoke(task, "CreateTaskReferenceHelper");
                check((bool)Invoke(task, "DoThreadJob")! && task.Info.FileName == "synthetic.png" && task.Data.Length > 0, "image processing changes BMP filename and bytes to PNG before upload");
                IGenericUploaderService? selected = null; string? selectedName = null;
                task.UploadConfirmation = (service, name) => { selected = service; selectedName = name; return false; };
                Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0);
                check(selected?.ServiceIdentifier == originalSecondary.ServiceIdentifier && selectedName == "synthetic.png", "consent uses processed extension and its actual filtered destination");
                check(task.StopRequested && !task.Info.Result.IsURLExpected, "processed upload can be cancelled without sending data");
            }

            UploaderFactory.FileUploaderServices[primaryKey] = primary;
            UploaderFactory.FileUploaderServices[secondaryKey] = secondary;
            var fallback = new TaskSettings { AfterCaptureJob = AfterCaptureTasks.None, AfterUploadJob = AfterUploadTasks.None, FileDestination = primaryKey };
            config.SecondaryImageUploaders = [ImageDestination.Imgur];
            config.SecondaryTextUploaders = [TextDestination.Pastebin];
            config.SecondaryFileUploaders = [secondaryKey];
            using (var task = WorkerTask.CreateDataUploaderTask(EDataType.File, new MemoryStream([4, 5]), "synthetic.bin", fallback))
            {
                var asked = new List<IGenericUploaderService>();
                task.UploadConfirmation = (service, _) => { asked.Add(service); return ReferenceEquals(service, primary); };
                Invoke(task, "CreateTaskReferenceHelper");
                check((bool)Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0)! && primary.Uploads == 1, "approved primary reaches memory uploader and produces a retryable failure");
                check(!(bool)Invoke(task, "DoUpload", task.Data, task.Info.FileName, 1)! && asked.SequenceEqual(new IGenericUploaderService[] { primary, secondary }), "retry resolves alternate destination and requests separate consent");
                check(task.StopRequested && secondary.ConfigChecks == 0 && secondary.Created == 0 && secondary.Uploads == 0, "alternate cancellation occurs before config check or uploader creation");
            }
            fallback.FileDestination = primaryKey;
            using (var task = WorkerTask.CreateDataUploaderTask(EDataType.File, new MemoryStream([6]), "retry.bin", fallback))
            {
                int calls = 0;
                task.UploadConfirmation = (_, _) => { calls++; return true; };
                Invoke(task, "CreateTaskReferenceHelper");
                Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0);
                Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0);
                check(calls == 1 && primary.Uploads == 3, "same destination retries retain task consent");
            }
            using (var task = WorkerTask.CreateDataUploaderTask(EDataType.File, new MemoryStream([7]), "next-task.bin", fallback))
            {
                int calls = 0;
                task.UploadConfirmation = (_, _) => { calls++; return false; };
                Invoke(task, "CreateTaskReferenceHelper");
                Invoke(task, "DoUpload", task.Data, task.Info.FileName, 0);
                check(calls == 1 && primary.Uploads == 3, "new task does not inherit previous task consent");
            }
        }
        finally
        {
            UploaderFactory.FileUploaderServices[primaryKey] = originalPrimary;
            UploaderFactory.FileUploaderServices[secondaryKey] = originalSecondary;
            foreach (var property in properties) property.Value.SetValue(null, previous[property.Key]);
        }
    }
}
