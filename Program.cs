using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;

namespace ChanThreadWatch {
	static class Program {
		private static Mutex _mutex;

		[STAThread]
		static void Main() {
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			if (!ObtainMutex()) {
				MessageBox.Show("Another instance of this program is running.", "Already Running", MessageBoxButtons.OK, MessageBoxIcon.Information);
				return;
			}
			Application.Run(new frmChanThreadWatch());
		}

		private static bool ObtainMutex() {
			SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
			MutexSecurity security = new MutexSecurity();
			bool createdNew;
			try {
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow));
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.ChangePermissions, AccessControlType.Deny));
				security.AddAccessRule(new MutexAccessRule(sid, MutexRights.Delete, AccessControlType.Deny));
			}
			catch (ArgumentOutOfRangeException) {
				// Work-around for Mono.  Just return here since Mutexes in Mono don't
				// work properly even with with default security.
				return true;
			}
			_mutex = new Mutex(false, @"Global\ChanThreadWatch", out createdNew, security);
			try {
				if (!_mutex.WaitOne(0, false)) {
					return false;
				}
			}
			catch (AbandonedMutexException) { }
			return true;
		}

		public static void ReleaseMutex() {
			if (_mutex == null) return;
			_mutex.ReleaseMutex();
			_mutex = null;
		}
	}
}