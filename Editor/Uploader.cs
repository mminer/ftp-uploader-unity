using System;
using System.IO;
using System.Net;
using UnityEditor;
using UnityEngine;

/// <summary>
/// An editor extension for uploading built files directly to a (S)FTP server.
/// </summary>
class FTPUploader : EditorWindow
{
	enum Protocol { FTP, SFTP }

	// Application info:

	public static readonly Version version = new Version(0, 1);
	const string applicationName = "Uploader";

	bool showMoreOptions;

	// Labels:

	GUIContent protocolLabel = new GUIContent("Protocol");
	GUIContent serverLabel = new GUIContent("Server");
	GUIContent usernameLabel = new GUIContent("User Name");
	GUIContent passwordLabel = new GUIContent("Password");
	GUIContent uploadLabel = new GUIContent("Upload");
	GUIContent moreOptionsLabel = new GUIContent("More Options");
	GUIContent pathLabel = new GUIContent("Initial Path", "The directory to upload to on the server.");
	GUIContent filenameLabel = new GUIContent("File Name", "The name of the .unity3d file that's created.");

	/// <summary>
	/// Initializes the window.
	/// </summary>
	[MenuItem("Window/Uploader")]
	static void Init ()
	{
		// Show existing open window, or make new one.
		Uploader window = EditorWindow.GetWindow(typeof(Uploader)) as Uploader;
		window.Show();
	}

	/// <summary>
	/// Displays the upload form.
	/// </summary>
	void OnGUI ()
	{
		// Fetch existing stored values.
		var protocol = (Protocol)EditorPrefs.GetInt(applicationName + " protocol", (int)Protocol.FTP);
		var server = EditorPrefs.GetString(applicationName + " server", "");
		var username = EditorPrefs.GetString(applicationName + " username", "");
		var password = EditorPrefs.GetString(applicationName + " password", "");
		var filename = EditorPrefs.GetString(applicationName + " filename", "game");
		var initialPath = EditorPrefs.GetString(applicationName + " initialpath", "");

		// Get new values.
		protocol = (Protocol)EditorGUILayout.EnumPopup(protocolLabel, protocol);
		server = EditorGUILayout.TextField(serverLabel, server);
		username = EditorGUILayout.TextField(usernameLabel, username);
		password = EditorGUILayout.PasswordField(passwordLabel, password);

		// More options:

		showMoreOptions = EditorGUILayout.Foldout(showMoreOptions, moreOptionsLabel);

		if (showMoreOptions) {
			filename = EditorGUILayout.TextField(filenameLabel, filename);
			initialPath	= EditorGUILayout.TextField(pathLabel, initialPath);
		}

		// Store new values.
		EditorPrefs.SetInt(applicationName + " protocol", (int)protocol);
		EditorPrefs.SetString(applicationName + " server", server);
		EditorPrefs.SetString(applicationName + " username", username);
		EditorPrefs.SetString(applicationName + " password", password);
		EditorPrefs.SetString(applicationName + " filename", filename);
		EditorPrefs.SetString(applicationName + " initialpath", initialPath);

		filename += ".unity3d";

		EditorGUILayout.BeginHorizontal();

			GUILayout.FlexibleSpace();

			if (GUILayout.Button(uploadLabel)) {
				try {
					Build(filename);

					switch (protocol) {
						case Protocol.FTP:
							FTPUpload(filename, server, username, password, initialPath);
							break;

						case Protocol.SFTP:
							SFTPUpload(filename, server, username, password, initialPath);
							break;
					}
				} catch (Exception e) {
					Debug.LogError("Unable to upload game: " + e.Message);
				}
			}

		EditorGUILayout.EndHorizontal();
	}

	/// <summary>
	/// Builds the web player.
	/// </summary>
	/// <param name="filename">The name of the file to build;</param>
	static void Build (string filename)
	{
		var result = BuildPipeline.BuildPlayer(new string[] {}, filename, BuildTarget.WebPlayer, BuildOptions.Development);

		if (result != "") {
			throw new Exception("Unable to build file: " + result);
		}
	}

	/// <summary>
	/// Uploads a file through FTP.
	/// </summary>
	/// <param name="filename">The path to the file to upload.</param>
	/// <param name="server">The server to use.</param>
	/// <param name="username">The username to use.</param>
	/// <param name="password">The password to use.</param>
	/// <param name="initialPath">The path on the server to upload to.</param>
	static void Upload (string filename, string server, string username, string password, string initialPath)
	{
		var file = new FileInfo(filename);
		var address = new Uri("ftp://" + server + "/" + Path.Combine(initialPath, file.Name));
		var request = FtpWebRequest.Create(address) as FtpWebRequest;

		// Upload options:

		// Provide credentials
		request.Credentials = new NetworkCredential(username, password);

		// Set control connection to closed after command execution
		request.KeepAlive = false;

		// Specify command to be executed
		request.Method = WebRequestMethods.Ftp.UploadFile;

		// Specify data transfer type
		request.UseBinary = true;

		// Notify server about size of uploaded file
		request.ContentLength = file.Length;

		// Set buffer size to 2KB.
		var bufferLength = 2048;
		var buffer = new byte[bufferLength];
		var contentLength = 0;

		// Open file stream to read file
		var fs = file.OpenRead();

		try {
			// Stream to which file to be uploaded is written.
			var stream = request.GetRequestStream();

			// Read from file stream 2KB at a time.
			contentLength = fs.Read(buffer, 0, bufferLength);

			// Loop until stream content ends.
			while (contentLength != 0) {
				//Debug.Log("Progress: " + ((fs.Position / fs.Length) * 100f));
				// Write content from file stream to FTP upload stream.
				stream.Write(buffer, 0, contentLength);
				contentLength = fs.Read(buffer, 0, bufferLength);
			}

			// Close file and request streams
			stream.Close();
			fs.Close();
		} catch (Exception e) {
			Debug.LogError("Error uploading file: " + e.Message);
			return;
		}

		Debug.Log("Upload successful.");
	}
}
