using UnityEngine;
using System.Net.Sockets;
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Text;
using Ionic.Zip;
using System.Linq;
using System.Threading;

public class InstallUtilities
{
    private static readonly string trialString = "\x74\x72\x69\x61\x6C\x2E\x70\x6E\x67"; // trial.png
    private static readonly string trialReplace = "notrialll";

    private static string TitleID
    {
        get
        {
            return PlayerSettings.PSVita.contentID.Substring(7, 9);
        }
    }

    private static void PackBuild(string buildDir)
    {
        string savePath = Path.Combine(Directory.GetParent(buildDir).ToString(), new DirectoryInfo(buildDir).Name + ".vpk");
        EditorUtility.DisplayProgressBar("Creating VPK...", "Adding files", 30);
        using (ZipFile zip = new ZipFile())
        {
            zip.ParallelDeflateThreshold = -1; // DotNetZip bugfix that corrupts DLLs / binaries http://stackoverflow.com/questions/15337186/dotnetzip-badreadexception-on-extract
            zip.AddDirectory(buildDir);
            zip.Save(savePath);
            EditorUtility.RevealInFinder(savePath);
        }
        Directory.Delete(buildDir, true);
        EditorUtility.ClearProgressBar();
    }

    // Prepare the build located in `buildDir`
    private static int PrepareBuild(string buildDir, bool cleanUnused = true)
    {
        // find our self file (the file that will be executed)
        string self = null;

        foreach (string fileName in Directory.GetFiles(buildDir))
        {
            if (fileName.ToLower().EndsWith(".self") || Path.GetFileName(fileName) == "eboot.bin")
                self = fileName;
        }

        if (self == null) // Self file not found
        {
            EditorUtility.DisplayDialog("Error preparing build", "Could not find a valid SELF file in the specified build directory\nAre you sure this is the right directory?", "OK");
            Debug.LogError("Could not find a valid SELF file in the specified build directory.");
            return -1;
        }

        try
        {
            using (FileStream selfFile = File.Open(Path.Combine(buildDir, self), FileMode.Open))
            {
                // mark the self as safe unless the user says otherwise
                if (!PSP2Tools.UnsafeBuild)
                {
                    EditorUtility.DisplayProgressBar("Preparing build...", "Marking SELF as safe", 0);
                    selfFile.Seek(0x80, SeekOrigin.Begin);
                    selfFile.WriteByte(0);
                }

                EditorUtility.DisplayProgressBar("Preparing build...", "Removing trial watermark", 20);

                selfFile.Seek(0, SeekOrigin.Begin);
                int matchNum = 0;
                int replaceMatchNum = 0;

                byte[] buff = new byte[8192];
                int ret = 0;

                do
                {
                    ret = selfFile.Read(buff, 0, buff.Length);
                    if (ret < 0) break;

                    for (int i = 0; i < buff.Length; i++)
                    {
                        if (buff[i] == trialString[matchNum])
                        {
                            matchNum++;
                            if (matchNum == trialString.Length)
                            {
                                long trialPosition = selfFile.Position + i - buff.Length - trialString.Length + 1;
                                long oldPosition = selfFile.Position;

                                selfFile.Position = trialPosition;
                                selfFile.Write(Encoding.ASCII.GetBytes(trialReplace), 0, trialReplace.Length);

                                selfFile.Position = oldPosition;

                                matchNum = 0;
                                ret = -1; // break outer loop
                                break;
                            }
                        }
                        else if(buff[i] == trialReplace[replaceMatchNum])
                        {
                            replaceMatchNum++;
                            if(replaceMatchNum == trialReplace.Length) // We've already patched this file before
                            {
                                ret = -1; // break outer loop
                                break;
                            }
                        }
                        else
                        {
                            matchNum = 0;
                            replaceMatchNum = 0;
                        }
                    }
                } while (ret > 0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(exception.Message);
            Debug.LogError("Could not write to SELF file");
            EditorUtility.ClearProgressBar();
            return -1;
        }

        if (Path.GetFileName(self) != "eboot.bin")
            File.Move(self, self.Replace(Path.GetFileName(self), "eboot.bin"));

        if(!cleanUnused)
        {
            EditorUtility.ClearProgressBar();
            return 0; // No need to do anything else if we've already built directly on there
        }

        EditorUtility.DisplayProgressBar("Preparing build...", "Removing unused files", 30);

        if (Directory.Exists(Path.Combine(buildDir, "SymbolFiles")))
            Directory.Delete(Path.Combine(buildDir, "SymbolFiles"), true);

        if (File.Exists(Path.Combine(buildDir, "configuration.psp2path")))
            File.Delete(Path.Combine(buildDir, "configuration.psp2path"));

        if (File.Exists(Path.Combine(buildDir, "ScriptLayoutHashes.txt")))
            File.Delete(Path.Combine(buildDir, "ScriptLayoutHashes.txt"));

        DeleteBatFiles(buildDir);

        EditorUtility.ClearProgressBar();

        return 0;
    }

    private static void DeleteBatFiles(string directory)
    {
        foreach (string dir in Directory.GetDirectories(directory))
            DeleteBatFiles(Path.Combine(directory, dir));

        foreach (string file in Directory.GetFiles(directory))
            if (file.ToLower().EndsWith(".bat"))
                File.Delete(file);
    }

    private static BuildReport InvokeBuild(out string buildDir, PSP2Tools.BuildDevice buildLocation = PSP2Tools.BuildDevice.PC)
    {
        if(buildLocation == PSP2Tools.BuildDevice.Vita)
        {
            string driveLetter = MountVitaUSB();
            if(driveLetter == null)
            {
                Debug.LogError("Failed to mount USB");
                buildDir = null;
                return null;
            }
            
            buildDir = string.Format("{0}PSP2Tools-Build/", driveLetter);

            if(Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
                
            Directory.CreateDirectory(buildDir);
            
        }
        else 
        {
            buildDir = EditorUtility.SaveFolderPanel("Select build directory", Directory.GetParent(Application.dataPath).ToString(), "");
            if (buildDir.Length == 0) return null;
        }
     

        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        string[] scenes = new string[sceneCount];
        for (int i = 0; i < sceneCount; i++)
        {
            scenes[i] = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
        }

        BuildReport result = BuildPipeline.BuildPlayer(scenes, buildDir, BuildTarget.PSP2, BuildOptions.None);

        return result;
    }

    private static string MountVitaUSB()
    {
        // First, figure out what device we should mount
        SendCommand("usb unmount");
        string device = "";

        // Check psvsd
        if (SendCommand("exists sdstor0:uma-pp-act-a").ToLower().Contains("exists") || SendCommand("exists sdstor0:uma-lp-act-entire").ToLower().Contains("exists"))
            device = "psvsd";
        // now sd2vita
        else if (SendCommand("exists sdstor0:gcd-lp-ign-entire").ToLower().Contains("exists"))
            device = "sd2vita";
        // now memory card
        else if (SendCommand("exists sdstor0:xmc-lp-ign-userext").ToLower().Contains("exists") || SendCommand("exists sdstor0:int-lp-ign-userext").ToLower().Contains("exists"))
            device = "memcard";

        // Figure out the drive letter
        string[] drivesBefore = Directory.GetLogicalDrives();

        string mountResult = SendCommand(string.Format("usb mount {0}", device));
        if (!mountResult.ToLower().Contains("success")) // check if mount failed
        {
            Debug.LogError(string.Format("Failed to mount USB mass storage\n{0}", mountResult));
            return null;
        }

        // Wait for the thing to actually mount
        short wait = 0;
        while (Directory.GetLogicalDrives().Length == drivesBefore.Length && wait < 4)
        {
            Thread.Sleep(1000);
            wait += 1;
        }

        string[] drivesAfter = Directory.GetLogicalDrives();
        if (drivesBefore.Length == drivesAfter.Length)
        {
            Debug.LogError("Timed out waiting for USB mount.");
            return null;
        }

        string driveLetter = null;
        foreach (string letter in drivesAfter)
        {
            if (!drivesBefore.Contains(letter))
                driveLetter = letter.Replace('\\', '/');
        }

        if (driveLetter == null) // How???
        {
            Debug.LogError("Failed to determine drive letter");
            return null;
        }

        return driveLetter;
    }

    private static void InstallBuild(string buildDir)
    {
        SendCommand("destroy");
        switch (PSP2Tools.TransferType)
        {
            case PSP2Tools.TransferMode.USB:
            {
                switch (PSP2Tools.BuildLocation)
                {
                    case PSP2Tools.BuildDevice.PC:
                        if(PSP2Tools.PackVPK)
                            PackBuild(buildDir);                            
                        
                        // Mount the vita
                        string driveLetter = MountVitaUSB();

                        if(driveLetter == null)
                        {
                            Debug.LogError("Failed to mount USB storage");
                            break;
                        }

                        if(PSP2Tools.PackVPK) // Simply copy VPK, unmount and install
                        {
                            EditorUtility.DisplayProgressBar("Installing...", "Copying VPK", 0);
                            string vpkPath = Path.Combine(Directory.GetParent(buildDir).ToString(), new DirectoryInfo(buildDir).Name + ".vpk");
                            File.Copy(vpkPath, Path.Combine(driveLetter, Path.GetFileName(vpkPath)));
                            
                            EditorUtility.DisplayProgressBar("Installing...", "Installing VPK", 0);
                            SendCommand("usb unmount");
                            
                            if(SendCommand(string.Format("vpk ux0:/{0}", Path.GetFileName(vpkPath))).ToLower().Contains("success"))
                                SendCommand(string.Format("launch {0}", TitleID));

                            EditorUtility.ClearProgressBar();
                        }
                        else // Copy the entire build folder :fear: then send the prom command 
                        {
                            string destination;

                            if(PSP2Tools.InstallMethod == PSP2Tools.InstallType.Complete)
                                destination = string.Format("{0}data/psp2toolsprom", driveLetter);
                            else destination = string.Format("{0}app/{1}", driveLetter, TitleID); 

                            EditorUtility.DisplayProgressBar("Installing...", "Copying Build", 0);

                            if(PSP2Tools.InstallMethod == PSP2Tools.InstallType.Compare)
                            {
                                try {
                                    TransferUtilities.UpdateDirectory(new DirectoryInfo(buildDir), new DirectoryInfo(destination));
                                } finally {
                                    EditorUtility.ClearProgressBar();
                                } 
                            }
                            else
                                TransferUtilities.CopyDirectory(buildDir, destination, true);

                            SendCommand("usb unmount");

                            EditorUtility.DisplayProgressBar("Installing...", "Installing build", 0);

                            if(PSP2Tools.InstallMethod == PSP2Tools.InstallType.Complete) // We need to promote the folder
                            {
                                string installResult = SendCommand("prom ux0:/data/psp2toolsprom");
                                if(!installResult.ToLower().Contains("success")) // Install failed
                                {
                                    EditorUtility.ClearProgressBar();
                                    Debug.Log(installResult);
                                    break;
                                }
                            }

                            SendCommand(string.Format("launch {0}", TitleID));
                            EditorUtility.ClearProgressBar();
                        }
                        break;

                    case PSP2Tools.BuildDevice.Vita:
                        SendCommand("usb unmount");
                        SendCommand("self ux0:/PSP2Tools-Build/eboot.bin");
                        break;
                }

                // Check which install method to use
                break;
            }
            case PSP2Tools.TransferMode.FTP:
                if (PSP2Tools.PackVPK)
                {
                    PackBuild(buildDir);
                    string vpkPath = Path.Combine(Directory.GetParent(buildDir).ToString(), new DirectoryInfo(buildDir).Name + ".vpk");
                    // Upload the vpk file & send command to install
                    TransferUtilities.UploadFTPFile(vpkPath, "ux0:/data", PSP2Tools.IPAddress, PSP2Tools.FTPPort.ToString());
                    EditorUtility.DisplayProgressBar("Installing", "Please wait...", 80);

                    if (SendCommand("vpk ux0:/data/" + Path.GetFileName(vpkPath)).ToLower().Contains("success"))
                        SendCommand(string.Format("launch {0}", TitleID));

                    EditorUtility.ClearProgressBar();
                }
                else
                {
                    switch (PSP2Tools.FTPInstallMethod)
                    {
                        case PSP2Tools.FTPInstallType.Complete:
                            // Upload the folder and & send promotion command
                            string promDirURL = "ftp://" + PSP2Tools.IPAddress + ":" + PSP2Tools.FTPPort + "/ux0:/data/psp2toolsprom";
                            TransferUtilities.CreateFolderFTP(promDirURL);
                            TransferUtilities.UploadFTPDirectory(buildDir, "ux0:/data/psp2toolsprom", PSP2Tools.IPAddress, PSP2Tools.FTPPort.ToString());
                            EditorUtility.DisplayProgressBar("Installing", "Please wait...", 80);
                            SendCommand("destroy");
                            if (SendCommand("prom ux0:/data/psp2toolsprom/").ToLower().Contains("success"))
                                SendCommand("launch " + TitleID);

                            EditorUtility.ClearProgressBar();
                            break;

                        case PSP2Tools.FTPInstallType.Copy:
                            // THIS IS BASICALLY UNTESTED BUT ITS SO SIMPLE IT SHOULD WORK
                            SendCommand("destory");
                            TransferUtilities.UploadFTPDirectory(buildDir, "ux0:/app/" + TitleID, PSP2Tools.IPAddress, PSP2Tools.FTPPort.ToString());
                            SendCommand("launch " + TitleID);
                            break;
                    }
                }
                break;
        }
    }

    [MenuItem("PSP2/Build and Install")]
    private static void BuildAndRun()
    {
        string buildDir;
        BuildReport buildResult = InvokeBuild(out buildDir, PSP2Tools.BuildLocation);

        if (buildResult.summary.result == BuildResult.Succeeded)
        {
            if (PrepareBuild(buildDir, PSP2Tools.BuildLocation == PSP2Tools.BuildDevice.PC) == 0)
                InstallBuild(buildDir);
        }
    }

    // This will build the project and pack it into a VPK file for distribution
    [MenuItem("PSP2/Build and Pack VPK")]
    private static void Build()
    {
        string buildDir;
        BuildReport buildResult = InvokeBuild(out buildDir);

        if (buildResult.summary.result == BuildResult.Succeeded)
        {
            if (PrepareBuild(buildDir) == 0)
                PackBuild(buildDir);
        }
    }

    // This installs the selected build
    [MenuItem("PSP2/Install")]
    public static void Install()
    {
        string buildDir = EditorUtility.SaveFolderPanel("Select build directory to install", Directory.GetParent(Application.dataPath).ToString(), "");

        if (buildDir.Length > 0)
        {
            if (PrepareBuild(buildDir) == 0)
                InstallBuild(buildDir);
        }
    }

    [MenuItem("PSP2/Pack VPK")]
    private static void PackVPK()
    {
        string buildDir = EditorUtility.SaveFolderPanel("Select build directory", Directory.GetParent(Application.dataPath).ToString(), "");
        if (buildDir.Length > 0)
        {
            if (PrepareBuild(buildDir) == 0)
                PackBuild(buildDir);
        }
    }

    [MenuItem("PSP2/Launch")]
    public static void Launch() { Debug.Log(SendCommand("launch " + TitleID)); }

    static string SendCommand(string cmd)
    {
        using (TcpClient client = new TcpClient(PSP2Tools.IPAddress, PSP2Tools.CMDPort))
        {
            using (NetworkStream ns = client.GetStream())
            {
                using (StreamWriter sw = new StreamWriter(ns))
                {
                    sw.Write(cmd + "\n");
                    sw.Flush();
                    using (StreamReader sr = new StreamReader(ns))
                    {
                        return sr.ReadToEnd().Remove(0, 2);
                    }
                }
            }
        }
    }
}
