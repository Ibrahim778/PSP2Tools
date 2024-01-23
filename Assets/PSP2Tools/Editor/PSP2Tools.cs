using UnityEngine;
using UnityEditor;
using System;

public class PSP2Tools : EditorWindow
{
    private static Vector2 scrollPos;

    public enum TransferMode
    {
        USB = 0,
        FTP = 1
    }

    public enum InstallType
    {
        Complete = 0,
        Compare = 1,
        Copy = 2
    }

    public enum FTPInstallType
    {
        Complete = 0,
        Copy = 1
    }

    public enum BuildDevice
    {
        PC = 0,
        Vita = 1
    }

    public static string IPAddress
    {
        get
        {
            return EditorPrefs.GetString("PSP2Tools/IP", "192.168.1.1");
        }
        set
        {
            EditorPrefs.SetString("PSP2Tools/IP", value);
        }
    }

    public static int CMDPort
    {
        get
        {
            return EditorPrefs.GetInt("PSP2Tools/CMD", 1338);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/CMD", value);
        }
    }

    public static int FTPPort
    {
        get
        {
            return EditorPrefs.GetInt("PSP2Tools/FTP", 1337);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/FTP", value);
        }
    }

    public static TransferMode TransferType
    {
        get
        {
            return (TransferMode)EditorPrefs.GetInt("PSP2Tools/TransferMode", (int)TransferMode.USB);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/TransferMode", (int)value);
        }
    }

    public static InstallType InstallMethod
    {
        get
        {
            return (InstallType)EditorPrefs.GetInt("PSP2Tools/InstallType", (int)InstallType.Compare);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/InstallType", (int)value);
        }
    }

    public static FTPInstallType FTPInstallMethod
    {
        get
        {
            return (FTPInstallType)EditorPrefs.GetInt("PSP2Tools/FTPInstallType", (int)FTPInstallType.Complete);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/FTPInstallType", (int)value);
        }
    }

    public static BuildDevice BuildLocation
    {
        get
        {
            return (BuildDevice)EditorPrefs.GetInt("PSP2Tools/BuildLocation", (int)BuildDevice.PC);
        }
        set
        {
            EditorPrefs.SetInt("PSP2Tools/BuildLocation", (int)value);
        }
    }

    public static bool PackVPK
    {
        get
        {
            return EditorPrefs.GetBool("PSP2/PackVPK", false);
        }

        set
        {
            EditorPrefs.SetBool("PSP2/PackVPK", value);
        }
    }

    public static bool UnsafeBuild
    {
        get
        {
            return EditorPrefs.GetBool("PSP2/UnsafeBuild", false);
        }

        set
        {
            EditorPrefs.SetBool("PSP2/UnsafeBuild", value);
        }
    }

    [MenuItem("PSP2/Settings")]
    public static void Settings() { GetWindow<PSP2Tools>("PSP2 Tools"); }

    void OnGUI()
    {
        // Our actual editor gui code goes here

#if !UNITY_PSP2
        EditorGUILayout.HelpBox("This tool is for the PlayStation Vita.\nTo use it switch the build platform to PSP2", MessageType.Error);
        return;
#endif

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, false, false);
        EditorGUILayout.BeginVertical();

        GUILayout.Label("Connection Settings", EditorStyles.boldLabel);
        GuiLine();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("IP Address");
        GUILayout.FlexibleSpace();
        IPAddress = EditorGUILayout.TextField(IPAddress, EditorStyles.numberField, new GUILayoutOption[] { GUILayout.MinWidth(112), GUILayout.MaxWidth(300) }).Split(' ')[0];

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("CMD Port  ");
        GUILayout.FlexibleSpace();
        CMDPort = int.Parse(EditorGUILayout.TextField(CMDPort.ToString(), EditorStyles.numberField, new GUILayoutOption[] { GUILayout.MinWidth(112), GUILayout.MaxWidth(300) }).Split(' ')[0]);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("FTP Port    ");
        GUILayout.FlexibleSpace();
        FTPPort = int.Parse(EditorGUILayout.TextField(FTPPort.ToString(), EditorStyles.numberField, new GUILayoutOption[] { GUILayout.MinWidth(112), GUILayout.MaxWidth(300) }).Split(' ')[0]);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        GUILayout.Label("Install Settings", EditorStyles.boldLabel);
        GuiLine();

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Transfer Mode");
        GUILayout.FlexibleSpace();
        TransferType = (TransferMode)EditorGUILayout.Popup((int)TransferType, Enum.GetNames(typeof(TransferMode)));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = BuildLocation == BuildDevice.PC || TransferType == TransferMode.FTP;

        GUILayout.Label("Pack VPK       ");
        GUILayout.FlexibleSpace();
        if (BuildLocation == BuildDevice.PC || TransferType == TransferMode.FTP)
            PackVPK = EditorGUILayout.Toggle(PackVPK);
        else
            EditorGUILayout.Toggle(false);

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();


        EditorGUILayout.HelpBox(PackVPK ? "Pack the build into a VPK for the vita to extract later  " : "Do not pack the files into a VPK for installation  ", MessageType.Info);

        if (TransferType == TransferMode.USB)
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Build Location ");
            GUILayout.FlexibleSpace();
            BuildLocation = (BuildDevice)EditorGUILayout.Popup((int)BuildLocation, Enum.GetNames(typeof(BuildDevice)));

            EditorGUILayout.EndHorizontal();

            if (BuildLocation == BuildDevice.Vita)
                EditorGUILayout.HelpBox("Build the project directly onto the vita and run the eboot.bin skipping installation  ", MessageType.Info);

            if (BuildLocation == BuildDevice.PC && !PackVPK)
            {
                EditorGUILayout.BeginHorizontal();

                GUILayout.Label("Install Type    ");
                GUILayout.FlexibleSpace();
                InstallMethod = (InstallType)EditorGUILayout.Popup((int)InstallMethod, Enum.GetNames(typeof(InstallType)));

                EditorGUILayout.EndHorizontal();

                switch (InstallMethod)
                {
                    case InstallType.Complete:
                        EditorGUILayout.HelpBox("Copy all files to vita and re-install bubble  ", MessageType.Info);
                        break;
                    case InstallType.Compare:
                        EditorGUILayout.HelpBox("Overwrite changed files (without re-installing)  ", MessageType.Info);
                        break;
                    case InstallType.Copy:
                        EditorGUILayout.HelpBox("Overwrite all previously installed files  ", MessageType.Info);
                        break;
                }
            }
        }
        else if(!PackVPK) // FTP
        {
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label("Install Type    ");
            GUILayout.FlexibleSpace();
            FTPInstallMethod = (FTPInstallType)EditorGUILayout.Popup((int)FTPInstallMethod, Enum.GetNames(typeof(FTPInstallType)));

            EditorGUILayout.EndHorizontal();

            switch (FTPInstallMethod)
            {
                case FTPInstallType.Complete:
                    EditorGUILayout.HelpBox("Copy all files to vita and re-install bubble  ", MessageType.Info);
                    break;
                    
                case FTPInstallType.Copy:
                    EditorGUILayout.HelpBox("Overwrite all previously installed files  ", MessageType.Info);
                    break;
            }
        }

        EditorGUILayout.BeginHorizontal();

        GUILayout.Label("Mark SELF as unsafe");
        GUILayout.FlexibleSpace();
        UnsafeBuild = EditorGUILayout.Toggle(UnsafeBuild);

        EditorGUILayout.EndHorizontal();

        if (UnsafeBuild)
            EditorGUILayout.HelpBox("This will cause the application to crash unless you have the CapUnlocker plugin installed\nDo not enable this unless you know what you're doing  ", MessageType.Warning);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        GUIUtility.ExitGUI();
    }

    void GuiLine(int i_height = 1)
    {
        Rect rect = EditorGUILayout.GetControlRect(false, i_height);
        rect.height = i_height;
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
    }
}
