﻿using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace JDP {
	internal static class Program {
		private static Mutex _mutex;

		[STAThread]
		private static void Main() {
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (!ObtainMutex()) {
				MessageBox.Show("Another instance of this program is running.", "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			Application.Run(new frmChanThreadWatch());
		}

		private static bool ObtainMutex() {
			return ObtainMutex(Settings.GetSettingsDirectory());
		}

		public static bool ObtainMutex(string settingsFolder) {
			SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
			MutexSecurity security = new MutexSecurity();
			bool useDefaultSecurity = false;
			try {
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.ChangePermissions, AccessControlType.Deny));
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.Delete, AccessControlType.Deny));
			}
			catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is NotImplementedException) {
				// Workaround for Mono
				useDefaultSecurity = true;
			}
			string name = @"Global\ChanThreadWatch_" + General.Calculate64BitMD5(Encoding.UTF8.GetBytes(
				settingsFolder.ToUpperInvariant())).ToString("X16");
			Mutex mutex = !useDefaultSecurity ?
				new Mutex(false, name, out bool _, security) :
				new Mutex(false, name);
			try {
				if (!mutex.WaitOne(0, false)) {
					return false;
				}
			}
			catch (AbandonedMutexException) { }
			ReleaseMutex();
			_mutex = mutex;
			return true;
		}

		public static void ReleaseMutex() {
			if (_mutex == null) return;
			try {
				_mutex.ReleaseMutex();
			}
			catch { }
			_mutex = null;
		}
	}
}
