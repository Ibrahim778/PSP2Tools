using System.Security.Cryptography;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System;

public class TransferUtilities : MonoBehaviour
{
    const int retryCount = 3;
    private static SHA1 sha1 = SHA1.Create(); 

    // Lol thanks stack overflow
    public static void CreateFolderFTP(string url)
    {
        try
        {
            FtpWebRequest requestDir = (FtpWebRequest)WebRequest.Create(url);
            requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
            requestDir.Credentials = new NetworkCredential("Anonymous", "b0ss");
            requestDir.GetResponse().Close();
        }
        catch (WebException ex)
        {
            FtpWebResponse response = (FtpWebResponse)ex.Response;
            if (response.StatusCode ==
                  FtpStatusCode.ActionNotTakenFileUnavailable || response.StatusCode == FtpStatusCode.ClosingData)
            {
                // probably exists already
            }
            else
            {
                throw;
            }
        }
    }

    public static void CopyStream(Stream input, Stream output)
    {
        byte[] buffer = new byte[32768];
        long written = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            output.Write(buffer, 0, read);
            written += read;
        }
    }

    static void IndexDirectory(string directory, ref List<string> outFiles, ref List<string> outDirectories)
    {
        foreach (string subDir in Directory.GetDirectories(directory))
        {
            IndexDirectory(subDir, ref outFiles, ref outDirectories);
            outDirectories.Add(subDir);
        }

        foreach (string file in Directory.GetFiles(directory))
            outFiles.Add(file);
    }

    static string GetRelativePath(string root, string path)
    {
        return path.Substring(root.Length, path.Length - root.Length).Replace('\\', '/');
    }

    public static void UploadFTPFile(string file, string remotePath, string ipadress, string port)
    {
        using (WebClient client = new WebClient())
        {
            client.Credentials = new NetworkCredential("Anonymous", "b0ss");

            string filename = Path.GetFileName(file);
            string uri = string.Format("ftp://{0}:{1}/{2}/{3}", ipadress, port, remotePath, filename);

            using (Stream remoteStream = client.OpenWrite(uri))
                using (FileStream fileStream = File.OpenRead(file))
                {
                    EditorUtility.DisplayProgressBar("Uploading file...", filename, 0);
                    CopyStream(fileStream, remoteStream);
                }
        }
    }

    public static void UploadFTPDirectory(string directory, string remotePath, string ipadress, string port)
    {
        List<string> files = new List<string>(); // Full path of all files to be uploaded
        List<string> directories = new List<string>(); // Full path of all directories that need to be made

        IndexDirectory(directory, ref files, ref directories);
        directories.Reverse();

        for (int i = 0; i < directories.Count; i++)
        {
            EditorUtility.DisplayProgressBar("Creating directories...", new DirectoryInfo(directories[i]).Name, i / directories.Count * 100f);
            string uri = string.Format("ftp://{0}:{1}/{2}{3}", ipadress, port, remotePath, GetRelativePath(directory, directories[i]));
            CreateFolderFTP(uri);
        }

        using (WebClient client = new WebClient())
        {
            client.Credentials = new NetworkCredential("Anonymous", "b0ss");
            for (int i = 0; i < files.Count; i++)
            {
                string relativePath = GetRelativePath(directory, files[i]);
                string uri = string.Format("ftp://{0}:{1}/{2}{3}", ipadress, port, remotePath, relativePath);
                string filename = Path.GetFileName(files[i]);
                Debug.Log(client);
                Debug.Log(uri);
                using (Stream remoteStream = client.OpenWrite(uri))
                using (FileStream fileStream = File.OpenRead(files[i]))
                {
                    EditorUtility.DisplayProgressBar("Uploading files...", filename, (float)i / (float)files.Count * 100f);
                    CopyStream(fileStream, remoteStream);
                }
            }
        }
    }

    // By @GlitcherOG
    public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
    {
        // Get information about the source directory
        var dir = new DirectoryInfo(sourceDir);

        // Check if the source directory exists
        if (!dir.Exists)
            throw new DirectoryNotFoundException("Source directory not found: " + dir.FullName);

        // Cache directories before we start copying
        DirectoryInfo[] dirs = dir.GetDirectories();

        // Create the destination directory
        Directory.CreateDirectory(destinationDir);

        // Get the files in the source directory and copy to the destination directory
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            EditorUtility.DisplayProgressBar("Copying files...", file.Name, 0);
            if(File.Exists(targetFilePath))
                File.Delete(targetFilePath);
            file.CopyTo(targetFilePath);
        }

        // If recursive and copying subdirectories, recursively call this method
        if (recursive)
        {
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir, true);
            }
        }
        EditorUtility.ClearProgressBar();
    }

    // From VitaFTPI
    public static void UpdateDirectory(DirectoryInfo directory1, DirectoryInfo directory2)
    {
        foreach (DirectoryInfo directoryInfo in directory1.GetDirectories())
        {
            if (!Directory.Exists(Path.Combine(directory2.FullName, directoryInfo.Name)))
                CopyDirectory(directoryInfo.FullName, Path.Combine(directory2.FullName, directoryInfo.Name), true);

            else
            {
                UpdateDirectory(directoryInfo, new DirectoryInfo(Path.Combine(directory2.FullName, directoryInfo.Name)));
            }
        }
        foreach (FileInfo file in directory1.GetFiles())
        {
            if (!File.Exists(Path.Combine(directory2.FullName, file.Name)))
            {
                EditorUtility.DisplayProgressBar("Copying files...", file.Name, 0);
                file.CopyTo(Path.Combine(directory2.FullName, file.Name));
            }
            else
            {
                if (file.Length.Equals(new FileInfo(Path.Combine(directory2.FullName, file.Name)).Length))
                {
                    EditorUtility.DisplayProgressBar("Comparing files...", file.Name, 0);
                    if (!Enumerable.SequenceEqual(GetSHA1Hash(file.FullName), GetSHA1Hash(Path.Combine(directory2.FullName, file.Name))))
                    {
                        EditorUtility.DisplayProgressBar("Copying files...", file.Name, 0);
                        file.CopyTo(Path.Combine(directory2.FullName, file.Name), true);
                    }
                }
                else
                {
                    file.CopyTo(Path.Combine(directory2.FullName, file.Name), true);
                }
            }
        }
    }

    private static byte[] GetSHA1Hash(string filename)
    {
            using (var bstream = new BufferedStream(File.OpenRead(filename), 100))
            {
                using (FileStream stream = File.OpenRead(filename))
                {
                    return sha1.ComputeHash(stream);
                }
            }
    }
}
