﻿using System;
using System.IO;
using System.Runtime.InteropServices;

using Mag.Shared;
using Mag.Shared.Settings;

using Decal.Adapter;

namespace MagFilter
{
	[FriendlyName("Mag-Filter")]
	public class FilterCore : FilterBase
	{
		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		internal static extern bool PostMessage(int hhwnd, uint msg, IntPtr wparam, UIntPtr lparam);


		readonly AutoRetryLogin autoRetryLogin = new AutoRetryLogin();
		readonly LoginCharacterTools loginCharacterTools = new LoginCharacterTools();
		readonly FastQuit fastQuit = new FastQuit();
		readonly LoginCompleteMessageQueueManager loginCompleteMessageQueueManager = new LoginCompleteMessageQueueManager();
		readonly AfterLoginCompleteMessageQueueManager afterLoginCompleteMessageQueueManager = new AfterLoginCompleteMessageQueueManager();

		DefaultFirstCharacterManager defaultFirstCharacterManager;
	    private LauncherChooseCharacterManager chooseCharacterManager;
		LoginNextCharacterManager loginNextCharacterManager;

        private string PluginName { get { return FileLocations.PluginName; } }
		protected override void Startup()
		{
            Debug.Init(FileLocations.PluginPersonalFolder.FullName + @"\Exceptions.txt", PluginName);
            SettingsFile.Init(FileLocations.GetPluginSettingsFile(), PluginName);
		    LogStartup();

			defaultFirstCharacterManager = new DefaultFirstCharacterManager(loginCharacterTools);
            chooseCharacterManager = new LauncherChooseCharacterManager(loginCharacterTools);
			loginNextCharacterManager = new LoginNextCharacterManager(loginCharacterTools);

			ClientDispatch += new EventHandler<NetworkMessageEventArgs>(FilterCore_ClientDispatch);
			ServerDispatch += new EventHandler<NetworkMessageEventArgs>(FilterCore_ServerDispatch);
			WindowMessage += new EventHandler<WindowMessageEventArgs>(FilterCore_WindowMessage);

			CommandLineText += new EventHandler<ChatParserInterceptEventArgs>(FilterCore_CommandLineText);
		}
        private void LogStartup()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

            log.WriteLogMsg(string.Format(
                "MagFilter.Startup, AssemblyVer: {0}, AssemblyFileVer: {1}",
                assembly.GetName().Version,
                System.Diagnostics.FileVersionInfo.GetVersionInfo(assembly.Location)
                                ));

        }

		protected override void Shutdown()
		{
			ClientDispatch -= new EventHandler<NetworkMessageEventArgs>(FilterCore_ClientDispatch);
			ServerDispatch -= new EventHandler<NetworkMessageEventArgs>(FilterCore_ServerDispatch);
			WindowMessage -= new EventHandler<WindowMessageEventArgs>(FilterCore_WindowMessage);

			CommandLineText -= new EventHandler<ChatParserInterceptEventArgs>(FilterCore_CommandLineText);
		}

		void FilterCore_ClientDispatch(object sender, NetworkMessageEventArgs e)
		{
			try
			{
				autoRetryLogin.FilterCore_ClientDispatch(sender, e);
				loginCompleteMessageQueueManager.FilterCore_ClientDispatch(sender, e);
				afterLoginCompleteMessageQueueManager.FilterCore_ClientDispatch(sender, e);
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}

		void FilterCore_ServerDispatch(object sender, NetworkMessageEventArgs e)
		{
			try
			{
				autoRetryLogin.FilterCore_ServerDispatch(sender, e);
				loginCharacterTools.FilterCore_ServerDispatch(sender, e);

				defaultFirstCharacterManager.FilterCore_ServerDispatch(sender, e);
                chooseCharacterManager.FilterCore_ServerDispatch(sender, e);
				loginNextCharacterManager.FilterCore_ServerDispatch(sender, e);
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}

		void FilterCore_WindowMessage(object sender, WindowMessageEventArgs e)
		{
			try
			{
				fastQuit.FilterCore_WindowMessage(sender, e);
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}

		void FilterCore_CommandLineText(object sender, ChatParserInterceptEventArgs e)
		{
			try
			{
				loginCompleteMessageQueueManager.FilterCore_CommandLineText(sender, e);
				afterLoginCompleteMessageQueueManager.FilterCore_CommandLineText(sender, e);

				defaultFirstCharacterManager.FilterCore_CommandLineText(sender, e);
                chooseCharacterManager.FilterCore_CommandLineText(sender, e);
				loginNextCharacterManager.FilterCore_CommandLineText(sender, e);
			}
			catch (Exception ex) { Debug.LogException(ex); }
		}
	}
}
