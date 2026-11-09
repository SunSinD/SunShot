
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShareX;
using ShareX.HelpersLib;

internal static class Checks
{
 static int checks;
 static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); checks++; }
 static void Reject(Action action, string label) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, label); }
 [STAThread]
 static void Main(string[] args)
 {
   if(args.Contains("--history-ui-only"))
   {
      HistoryLifecycleChecks.RunUi(Check);
      Console.WriteLine($"{checks} checks passed");
      return;
   }
   if(args.Contains("--write-baseline"))
   {
      Directory.CreateDirectory(args[1]);
      foreach(var item in new (string,object)[]{("ApplicationConfig.json",new ApplicationConfig()),("HotkeysConfig.json",new HotkeysConfig()),("UploadersConfig.json",new ShareX.UploadersLib.UploadersConfig())})
         File.WriteAllText(Path.Combine(args[1],item.Item1),JsonConvert.SerializeObject(item.Item2,new JsonSerializerSettings{ContractResolver=new WritablePropertiesOnlyResolver(),Converters={new Newtonsoft.Json.Converters.StringEnumConverter()},Formatting=Formatting.Indented}));
      return;
   }
   string root = Path.Combine(Path.GetTempPath(), "sunshot-core-checks-" + Guid.NewGuid().ToString("N"));
   string source=Path.Combine(root,"ShareX"), destination=Path.Combine(root,"SunShot");
   Directory.CreateDirectory(source); Directory.CreateDirectory(destination);
   string appPath=Path.Combine(source,"ApplicationConfig.json");
   JObject app=new JObject {
     ["ApplicationVersion"]="21.0.0", ["DefaultTaskSettings"]=new JObject {
       ["AfterCaptureJob"]="CopyImageToClipboard, SaveImageToFile, UploadImageToHost, PerformActions, DeleteFile, AnalyzeImage",
       ["WatchFolderEnabled"]=true, ["WatchFolderList"]=new JArray(new JObject{["FolderPath"]="C:\\ShareX\\watch"}),
       ["OverrideScreenshotsFolder"]=true, ["ScreenshotsFolder"]="C:\\ShareX\\captures"
     }, ["CustomUploadersConfigPath"]="C:\\ShareX", ["CustomHotkeysConfigPath"]="C:\\ShareX",
     ["UseCustomScreenshotsPath"]=true, ["CustomScreenshotsPath"]="C:\\ShareX", ["AutoCheckUpdate"]=true,
     ["ActionsToolbarRunAtStartup"]=true, ["DisableHotkeys"]=false
   };
   File.WriteAllText(appPath,app.ToString());
   JObject hotkeys = new JObject { ["ApplicationVersion"]="21.0.0", ["Hotkeys"] = new JArray(new JObject {
     ["HotkeyInfo"]=new JObject { ["Hotkey"]="PrintScreen" }, ["TaskSettings"]=new JObject { ["Description"]="my workflow", ["Job"]="RectangleRegion", ["GeneralSettings"]=JValue.CreateNull(), ["UseDefaultGeneralSettings"]=true, ["ImageDestination"]="CustomImageUploader", ["AfterCaptureJob"]="UploadImageToHost, CopyImageToClipboard" }
   }) };
   File.WriteAllText(Path.Combine(source,"HotkeysConfig.json"),hotkeys.ToString());
   string credential="$DPAPIEncrypted$"+DPAPI.Encrypt("synthetic-secret");
   JObject uploaders=new JObject { ["ApplicationVersion"]="21.0.0", ["VgymeUserKey"]=credential, ["Paste_eeUserKey"]="$DPAPIEncrypted$broken" };
   File.WriteAllText(Path.Combine(source,"UploadersConfig.json"),uploaders.ToString());
   File.WriteAllText(Path.Combine(source,"PersonalPath.cfg"),"do not copy");
   File.WriteAllText(Path.Combine(source,"History.json"),"do not copy");
   File.WriteAllText(Path.Combine(source,"History.db"),"do not copy");
   File.WriteAllText(Path.Combine(destination,"ApplicationConfig.json"),"original-app");
   File.WriteAllText(Path.Combine(destination,"History.db"),"original-history");
   var originals=Directory.GetFiles(source).ToDictionary(Path.GetFileName,File.ReadAllBytes);
   var plan=ImportService.Prepare(source,destination);
   Check(Directory.GetFiles(destination).Length==2 && File.ReadAllText(Path.Combine(destination,"ApplicationConfig.json"))=="original-app", "prepare does not mutate destination");
   Check(plan.Files.Count==3 && plan.HotkeyCount==1 && plan.HasUploaders, "exactly three settings files and workflow preserved");
   JObject clean=JObject.Parse(Encoding.UTF8.GetString(plan.Files["ApplicationConfig.json"]));
   Check((bool)clean["DisableHotkeys"] && (bool)clean["DisableUpload"] && !(bool)clean["AutoCheckUpdate"] && !(bool)clean["ActionsToolbarRunAtStartup"], "hotkeys uploads updates and startup actions disabled");
   Check((string)clean["CustomUploadersConfigPath"]=="" && !(bool)clean["UseCustomScreenshotsPath"] && (string)clean["DefaultTaskSettings"]["ScreenshotsFolder"]=="", "shared config and screenshot paths reset");
   string capture=(string)clean["DefaultTaskSettings"]["AfterCaptureJob"];
   Check(!capture.Contains("Upload") && !capture.Contains("PerformActions") && !capture.Contains("DeleteFile") && !capture.Contains("AnalyzeImage") && capture.Contains("SaveImageToFile"), "automatic actions stripped while local capture retained");
   Check(!(bool)clean["DefaultTaskSettings"]["WatchFolderEnabled"] && !clean["DefaultTaskSettings"]["WatchFolderList"].Any(), "watch folders excluded");
   Check((string)JObject.Parse(Encoding.UTF8.GetString(plan.Files["HotkeysConfig.json"]))["Hotkeys"][0]["TaskSettings"]["Description"]=="my workflow", "workflow description preserved");
   JObject creds=JObject.Parse(Encoding.UTF8.GetString(plan.Files["UploadersConfig.json"]));
   Check((string)creds["VgymeUserKey"]==credential && (string)creds["Paste_eeUserKey"]=="" && plan.UnreadableCredentials==1, "same-user encrypted credentials retained; unreadable credentials cleared");
   Check(plan.HasHotkeys && plan.Warnings.Any(x=>x.Contains("CustomHotkeysConfigPath")) && plan.Warnings.Any(x=>x.Contains("CustomUploadersConfigPath")), "external paths skipped with explicit per-profile warnings");
   Check(clean["QuickTaskPresets"].Count(x=> (string)x["AfterCaptureTasks"] == "None") == 1 &&
      clean["QuickTaskPresets"].All(x=>x["Name"].Type==JTokenType.Null || !((string)x["Name"]).Contains("Upload")), "sanitized presets retain only intentional separator and no misleading upload names");
   string backup=ImportService.Apply(plan);
   Check(File.ReadAllText(Path.Combine(backup,"ApplicationConfig.json.original"))=="original-app", "existing SunShot profile backed up");
   Check(File.Exists(Path.Combine(backup,"HotkeysConfig.json.absent")) && !ImportService.HasPendingImport(destination), "absent targets tracked and successful transaction finished");
   Check(File.ReadAllText(Path.Combine(destination,"History.db"))=="original-history" && !File.Exists(Path.Combine(destination,"PersonalPath.cfg")) && !File.Exists(Path.Combine(destination,"History.json")), "history and personal path files untouched");
   Check(originals.All(x=>File.ReadAllBytes(Path.Combine(source,x.Key)).SequenceEqual(x.Value)), "all ShareX source files byte-identical");
   var loadedUploaders=ShareX.UploadersLib.UploadersConfig.Load(Path.Combine(destination,"UploadersConfig.json"),fallbackSupport:false);
   var loadedApp=ApplicationConfig.Load(Path.Combine(destination,"ApplicationConfig.json"),fallbackSupport:false);
   var loadedHotkeys=HotkeysConfig.Load(Path.Combine(destination,"HotkeysConfig.json"),fallbackSupport:false);
   Check(loadedUploaders.VgymeUserKey=="synthetic-secret" && loadedUploaders.Paste_eeUserKey=="" && loadedApp.DisableUpload && loadedApp.DisableHotkeys &&
      loadedHotkeys.Hotkeys.Count==1 && loadedHotkeys.Hotkeys[0].TaskSettings.Description=="my workflow", "imported files roundtrip through real SettingsBase.Load and DPAPI credential decryption");
   Reject(()=>ImportService.Prepare(source,source),"same folder rejected");
   Reject(()=>ImportService.Prepare(source,Path.Combine(source,"nested")),"nested destination rejected");
   string good=File.ReadAllText(appPath);
   File.WriteAllText(appPath,good.Replace("21.0.0","22.5.0")); Reject(()=>ImportService.Prepare(source,destination),"future schema rejected");
   File.WriteAllText(appPath,good.Replace("21.0.0","20.0.0")); Reject(()=>ImportService.Prepare(source,destination),"older schema rejected");
   File.WriteAllText(appPath,"{\"ApplicationVersion\":\"21.0.0\",\"DefaultTaskSettings\":{},\"ApplicationVersion\":\"21.0.0\"}"); Reject(()=>ImportService.Prepare(source,destination),"duplicate JSON properties rejected");
   File.WriteAllText(appPath,good+"{}"); Reject(()=>ImportService.Prepare(source,destination),"trailing JSON rejected");
   File.WriteAllText(appPath,good.Replace("\"DefaultTaskSettings\": {","\"DefaultTaskSettings\": {\"$type\":\"unexpected\",")); Reject(()=>ImportService.Prepare(source,destination),"JSON type metadata rejected");
   File.WriteAllText(appPath,good.Replace("CopyImageToClipboard, SaveImageToFile, UploadImageToHost, PerformActions, DeleteFile, AnalyzeImage","NotAnAction")); Reject(()=>ImportService.Prepare(source,destination),"invalid enum rejected");
   File.WriteAllText(appPath,good);
   // Simulate a process interruption after one target was replaced.
   string crashBackup=Path.Combine(destination,"Backup","ShareX-import-synthetic-crash"); Directory.CreateDirectory(crashBackup);
   foreach(string name in plan.Files.Keys) File.Copy(Path.Combine(destination,name),Path.Combine(crashBackup,name+".original"));
   byte[] previous=File.ReadAllBytes(Path.Combine(destination,"ApplicationConfig.json"));
   File.WriteAllText(Path.Combine(destination,"ApplicationConfig.json"),"partially-replaced");
   File.WriteAllText(Path.Combine(destination,".SunShot-import-pending.json"),JsonConvert.SerializeObject(Path.GetFileName(crashBackup)));
   ImportService.RecoverPendingImport(destination);
   Check(File.ReadAllBytes(Path.Combine(destination,"ApplicationConfig.json")).SequenceEqual(previous) && !ImportService.HasPendingImport(destination), "interrupted transaction restored before load");
   // A real OS sharing violation prevents import; recovery finishes after the lock is released.
   bool failed=false;
   using(var locked=new FileStream(Path.Combine(destination,"HotkeysConfig.json"),FileMode.Open,FileAccess.Read,FileShare.None))
   { try {ImportService.Apply(plan);} catch {failed=true;} }
   Check(failed,"locked destination import fails safely");
   ImportService.RecoverPendingImport(destination);
   Check(File.ReadAllBytes(Path.Combine(destination,"ApplicationConfig.json")).SequenceEqual(previous),"failed import leaves existing profile unchanged");
   // No external files are followed when optional config files are absent.
   File.Delete(Path.Combine(source,"HotkeysConfig.json")); File.Delete(Path.Combine(source,"UploadersConfig.json"));
   var missing=ImportService.Prepare(source,destination);
   Check(!missing.HasUploaders && !missing.HasHotkeys && missing.HotkeyCount>0 && missing.Files.Count==3,"missing optional files accurately reported as isolated defaults");
   JObject ffmpegFixture=JObject.Parse(good);
   ffmpegFixture["DefaultTaskSettings"]["CaptureSettings"]=new JObject{["FFmpegOptions"]=new JObject{["OverrideCLIPath"]=true,["CLIPath"]=Path.Combine(source,"Tools","ffmpeg.exe"),["VideoSource"]="Synthetic camera",["AudioSource"]="Synthetic microphone"}};
   File.WriteAllText(appPath,ffmpegFixture.ToString());
   var isolatedRecorder=ImportService.Prepare(source,destination);
   JObject isolatedApp=JObject.Parse(Encoding.UTF8.GetString(isolatedRecorder.Files["ApplicationConfig.json"]));
   Check(!(bool)isolatedApp["DefaultTaskSettings"]["CaptureSettings"]["FFmpegOptions"]["OverrideCLIPath"] &&
       (string)isolatedApp["DefaultTaskSettings"]["CaptureSettings"]["FFmpegOptions"]["CLIPath"]=="" &&
       (string)isolatedApp["DefaultTaskSettings"]["CaptureSettings"]["FFmpegOptions"]["VideoSource"]=="Synthetic camera" &&
       (string)isolatedApp["DefaultTaskSettings"]["CaptureSettings"]["FFmpegOptions"]["AudioSource"]=="Synthetic microphone" &&
       isolatedRecorder.Warnings.Any(x=>x.Contains("FFmpeg override") && x.Contains("Choose the existing ffmpeg.exe")), "source profile FFmpeg path is cleared and 21 device names are retained with guidance");
   File.WriteAllText(appPath,good);

   File.WriteAllText(appPath,good.Replace("\"ActionsToolbarRunAtStartup\": true", "\"Themes\": null,\"ActionsToolbarRunAtStartup\": true"));
   Reject(()=>ImportService.Prepare(source,destination),"null required containers rejected");
   File.WriteAllText(appPath,good);
   File.WriteAllText(Path.Combine(destination,".SunShot-import-pending.json"),JsonConvert.SerializeObject("../outside"));
   Reject(()=>ImportService.RecoverPendingImport(destination),"recovery journal path traversal rejected");
   File.Delete(Path.Combine(destination,".SunShot-import-pending.json"));
   app["UseMachineSpecificUploadersConfig"]=true;
   File.WriteAllText(appPath,app.ToString());
   string machineFile=Path.Combine(source,"UploadersConfig-"+FileHelpers.SanitizeFileName(Environment.MachineName.ToLowerInvariant())+".json");
   File.WriteAllText(machineFile,uploaders.ToString());
   var machine=ImportService.Prepare(source,destination);
   Check(machine.HasUploaders && !JObject.Parse(Encoding.UTF8.GetString(machine.Files["ApplicationConfig.json"]))["UseMachineSpecificUploadersConfig"].Value<bool>(),"machine-specific config imported into isolated default filename");
   string oversized=Path.Combine(source,"HotkeysConfig.json");
   using (var stream=new FileStream(oversized,FileMode.Create)) stream.SetLength(16*1024*1024+1);
   Reject(()=>ImportService.Prepare(source,destination),"oversized config rejected");
   File.Delete(oversized);
   Check(new ApplicationConfig().Language == SupportedLanguage.English, "new profiles default to English regardless of Windows language");
   using(var serialized = new ApplicationConfig().SaveToMemoryStream(false))
   {
      JObject full = JObject.Parse(Encoding.UTF8.GetString(serialized.ToArray()));
      full["ApplicationVersion"] = "21.0.0";
      full["SaveImageSubFolderPattern"] = @"..\..\ShareX\Screenshots";
      full["SaveImageSubFolderPatternWindow"] = @"C:\ShareX\Screenshots";
      File.WriteAllText(appPath, full.ToString());
      var complete = ImportService.Prepare(source, destination);
      JObject imported = JObject.Parse(Encoding.UTF8.GetString(complete.Files["ApplicationConfig.json"]));
      JArray effects = (JArray)imported["DefaultTaskSettings"]["ImageSettings"]["ImageEffectPresets"][0]["Effects"];
      Check(effects.Count == 2 && effects.All(x => x["$type"] != null), "complete production-serialized settings preserve allowed image effects");
      Check((string)imported["SaveImageSubFolderPattern"] == "%y-%mo" && (string)imported["SaveImageSubFolderPatternWindow"] == "", "import clears absolute and traversing capture subfolders");
      full["DefaultTaskSettings"]["ImageSettings"]["ImageEffectPresets"][0]["Effects"][0]["$type"] = "System.Windows.Data.ObjectDataProvider, PresentationFramework";
      File.WriteAllText(appPath, full.ToString());
      Reject(()=>ImportService.Prepare(source,destination), "image effect metadata cannot instantiate an arbitrary type");
      File.WriteAllText(appPath, app.ToString());
   }
   string link=Path.Combine(root,"source-link");
   try { Directory.CreateSymbolicLink(link,source); Reject(()=>ImportService.Prepare(link,destination),"symbolic link profile rejected"); }
   catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException) { Console.WriteLine("SKIP symbolic links unavailable in this Windows token"); }

   string fixtureJunction=Path.Combine(root,"junction");
   ProcessStartInfo junctionCommand=new ProcessStartInfo("powershell.exe") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
   junctionCommand.ArgumentList.Add("-NoProfile"); junctionCommand.ArgumentList.Add("-NonInteractive"); junctionCommand.ArgumentList.Add("-Command");
   junctionCommand.ArgumentList.Add("New-Item -ItemType Junction -Path '"+fixtureJunction.Replace("'","''")+"' -Target '"+source.Replace("'","''")+"' | Out-Null");
   using(Process process=Process.Start(junctionCommand))
   {
      string output=process.StandardOutput.ReadToEnd(); string error=process.StandardError.ReadToEnd(); process.WaitForExit();
      Check(process.ExitCode==0,"synthetic Windows junction fixture created");
   }
   Reject(()=>ImportService.Prepare(fixtureJunction,destination),"Windows junction profile rejected");
   ShareX22Checks.Run(root, Check, Reject);
   HistoryImportChecks.Run(root, Check, Reject);
   HistoryLifecycleChecks.Run(root, Check);
   if(args.Contains("--import-only")) { Console.WriteLine("CHECKS "+checks+"; synthetic files: "+root); return; }
   Assembly main=typeof(ApplicationConfig).Assembly;
   var flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
   Type program=main.GetType("ShareX.Program");
   Check((string)program.GetField("AppName",flags).GetRawConstantValue()=="SunShot", "production application name independent");
   Check((string)program.GetField("MutexName",flags).GetRawConstantValue()!="82E6AC09-0FEF-4390-AD9F-0DD3F5561EFC", "production mutex differs from ShareX");
   Check(((string)program.GetField("PipeName",flags).GetValue(null)).EndsWith("-SunShot"),"production named pipe independent");
   Check((string)program.GetField("PersonalPathConfigFileName",flags).GetRawConstantValue()=="SunShotPersonalPath.cfg", "portable path config cannot reuse ShareX PersonalPath.cfg");

   Type integrations=main.GetType("ShareX.IntegrationHelpers");
   string Integration(string name)=>(string)integrations.GetField(name,flags).GetValue(null);
   Check(Integration("ShellExtMenuName")=="SunShot" && Integration("ShellExtEditName")=="SunShotImageEditor","shell menu identifiers independent");
   Check(Integration("ShellCustomUploaderExtensionValue")=="SunShot.sxcu" && Integration("ShellImageEffectExtensionValue")=="SunShot.sxie","file association ProgIDs independent");
   Check(Integration("ShellCustomUploaderExtensionPath").EndsWith(@".sxcu\OpenWithProgids") && Integration("ShellImageEffectExtensionPath").EndsWith(@".sxie\OpenWithProgids"),"file associations add OpenWith entries instead of replacing defaults");
   Check(Integration("ChromeNativeMessagingHosts").EndsWith("com.sunshot.sunshot") && Integration("FirefoxNativeMessagingHosts").EndsWith("SunShot"),"browser native-host identifiers independent");
   Check((string)main.GetType("ShareX.SystemOptions").GetField("RegistryPath",flags).GetRawConstantValue()==@"SOFTWARE\SunShot","policy registry path independent");
   Check(((string)program.GetField("DefaultPersonalFolder",flags).GetValue(null)).EndsWith(@"\SunShot") && ((string)program.GetField("PortablePersonalFolder",flags).GetValue(null)).EndsWith(@"\SunShot"),"installed and portable profile directory names independent");
   Check(Path.GetFileName((string)program.GetProperty("LogsFilePath",flags).GetValue(null)).StartsWith("SunShot-Log-"),"log filename independent");
   Type updater=main.GetType("ShareX.ShareXUpdateManager");
   object manager=Activator.CreateInstance(updater,true);
   foreach(UpdateChannel channel in Enum.GetValues<UpdateChannel>())
   {
      updater.GetProperty("UpdateChannel").SetValue(manager,channel);
      GitHubUpdateChecker checker=(GitHubUpdateChecker)updater.GetMethod("CreateUpdateChecker").Invoke(manager,null);
      Check(checker.Owner=="SunSinD" && checker.Repo=="SunShot" && !checker.IsDev,"updater channel "+channel+" only queries SunSinD/SunShot");
   }
   object currentChecker=updater.GetMethod("CreateUpdateChecker").Invoke(manager,null);
   Type releaseType=typeof(GitHubUpdateChecker).GetNestedType("GitHubRelease",BindingFlags.NonPublic);
   Type assetType=typeof(GitHubUpdateChecker).GetNestedType("GitHubAsset",BindingFlags.NonPublic);
   object release=Activator.CreateInstance(releaseType,true);
   releaseType.GetProperty("tag_name").SetValue(release,"v0.1.0");
   object asset=Activator.CreateInstance(assetType,true);
   assetType.GetProperty("name").SetValue(asset,"SunShot-0.1.0-setup-x64.exe");
   assetType.GetProperty("url").SetValue(asset,"https://api.github.com/repos/SunSinD/SunShot/releases/assets/123");
   assetType.GetProperty("browser_download_url").SetValue(asset,"https://github.com/SunSinD/SunShot/releases/download/v0.1.0/SunShot-0.1.0-setup-x64.exe");
   Array assets=Array.CreateInstance(assetType,1);assets.SetValue(asset,0);releaseType.GetProperty("assets").SetValue(release,assets);
   MethodInfo update=currentChecker.GetType().GetMethod("UpdateReleaseInfo",BindingFlags.Instance|BindingFlags.NonPublic);
   Check((bool)update.Invoke(currentChecker,new object[]{release,false,false}),"planned setup asset selected");
   assetType.GetProperty("name").SetValue(asset,"ShareX-21.0.0-setup-x64.exe");
   Check(!(bool)update.Invoke(currentChecker,new object[]{release,false,false}),"ShareX-named release asset rejected");
   assetType.GetProperty("name").SetValue(asset,"SunShot-0.1.0-portable-x64.zip");
   assetType.GetProperty("browser_download_url").SetValue(asset,"https://github.com/SunSinD/SunShot/releases/download/v0.1.0/SunShot-0.1.0-portable-x64.zip");
   Check((bool)update.Invoke(currentChecker,new object[]{release,true,true}),"planned portable asset selected");
   assetType.GetProperty("browser_download_url").SetValue(asset,"https://github.com/ShareX/ShareX/releases/download/v21/ShareX.zip");
   Check(!(bool)update.Invoke(currentChecker,new object[]{release,true,true}),"upstream asset download URL rejected");


   HistoryLifecycleChecks.RunUi(Check);
   foreach(string language in new[]{"en-CA","fr-CA"})
   {
      System.Globalization.CultureInfo.CurrentUICulture=System.Globalization.CultureInfo.GetCultureInfo(language);
      using(var form=new ShareXSettingsImportForm())
      {
         form.CreateControl(); form.PerformLayout();
         foreach(System.Windows.Forms.Control child in form.Controls)
         {
            child.CreateControl(); child.PerformLayout();
            foreach(System.Windows.Forms.Control item in child.Controls)
               if(item is System.Windows.Forms.Label label)
                  Check(label.Height>=label.GetPreferredSize(new System.Drawing.Size(label.Width,0)).Height,"import form text fits: "+language);
         }
      }
   }
   // Real production SettingManager with all paths redirected to a synthetic profile.
   program.GetProperty("CustomPersonalPath",flags).SetValue(null,destination);
   ApplicationConfig live=new ApplicationConfig();
   program.GetProperty("Settings",flags).SetValue(null,live);
   program.GetProperty("DefaultTaskSettings",flags).SetValue(null,live.DefaultTaskSettings);
   program.GetProperty("UploadersConfig",flags).SetValue(null,new ShareX.UploadersLib.UploadersConfig());
   program.GetProperty("HotkeysConfig",flags).SetValue(null,new HotkeysConfig());
   Type settings=main.GetType("ShareX.SettingManager");
   live.ApplicationVersion = "0.0.1";
   object scrolling = live.DefaultTaskSettings.CaptureSettings.ScrollingCaptureOptions;
   settings.GetMethod("ApplicationConfigBackwardCompatibilityTasks", flags).Invoke(null,null);
   Check(ReferenceEquals(scrolling, live.DefaultTaskSettings.CaptureSettings.ScrollingCaptureOptions), "SunShot upgrades cannot rerun ShareX scrolling migrations");
   for(int i=0;i<12;i++) settings.GetMethod("SaveAllSettingsAsync",flags).Invoke(null,null);
   settings.GetMethod("SuspendSavesForImport",flags).Invoke(null,null);
   ImportService.Apply(machine);
   var frozen=machine.Files.Keys.ToDictionary(x=>x,x=>File.ReadAllBytes(Path.Combine(destination,x)));
   settings.GetMethod("SaveAllSettings",flags).Invoke(null,null);
   for(int i=0;i<12;i++) settings.GetMethod("SaveAllSettingsAsync",flags).Invoke(null,null);
   Thread.Sleep(300);
   Check(frozen.All(x=>File.ReadAllBytes(Path.Combine(destination,x.Key)).SequenceEqual(x.Value)),"production sync and queued async saves cannot overwrite imported settings");
   Console.WriteLine("CHECKS "+checks+"; synthetic files: "+root);

 }
}

internal static class ImportService
{
    private static readonly Type Service=typeof(ApplicationConfig).Assembly.GetType("ShareX.ShareXSettingsImportService",true);
    public sealed class Plan
    {
        internal readonly object Value;
        internal Plan(object value) { Value=value; }
        public Dictionary<string,byte[]> Files => (Dictionary<string,byte[]>)Value.GetType().GetField("Files",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Value);
        private T Get<T>(string name) => (T)Value.GetType().GetProperty(name).GetValue(Value);
        public int HotkeyCount => Get<int>("HotkeyCount");
        public int UnreadableCredentials => Get<int>("UnreadableCredentials");
        public bool HasUploaders => Get<bool>("HasUploaders");
        public bool HasHotkeys => Get<bool>("HasHotkeys");
        public bool HistoryRequested => Get<bool>("HistoryRequested");
        public bool HasHistory => Get<bool>("HasHistory");
        public int HistoryCount => Get<int>("HistoryCount");
        public bool ReusesCaptureFolder => Get<bool>("ReusesCaptureFolder");
        public string CaptureFolder => Get<string>("CaptureFolder");
        public List<string> Warnings => Get<List<string>>("Warnings");
    }
    private static object Call(string method,params object[] args)
    {
        try { return Service.GetMethod(method,BindingFlags.Static|BindingFlags.Public).Invoke(null,args); }
        catch(TargetInvocationException error) when(error.InnerException!=null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    public static Plan Prepare(string source,string destination) => new Plan(Call("Prepare",source,destination));
    public static Plan PrepareMigration(string source,string destination,bool history,bool capture) => new Plan(Call("PrepareMigration",source,destination,history,capture));
    public static void ValidateDestinationForImport(string source,string destination) => Call("ValidateDestinationForImport",source,destination);
    public static string Apply(Plan plan) => (string)Call("Apply",plan.Value);
    public static void RecoverPendingImport(string destination) => Call("RecoverPendingImport",destination);
    public static bool HasPendingImport(string destination) => (bool)Call("HasPendingImport",destination);
}
