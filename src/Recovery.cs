using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace ScreenCare
{
    internal static class Recovery
    {
        // Recover only expected operational/UI failures, never corrupted process state.
        internal static bool CanHandle(Exception error)
        {
            if (error is SEHException) return false;
            return error is IOException || error is UnauthorizedAccessException
                || error is SecurityException || error is ExternalException
                || error is InvalidOperationException || error is ArgumentException;
        }
        internal static void Attempt(Action action)
        {
            try { action(); }
            catch (Exception error)
            {
                if (!CanHandle(error)) throw;
                Diagnostics.Error(error);
            }
        }
    }
}
