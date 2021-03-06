﻿// Copyright (c) Igor Velikorossov. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Git Extensions. All rights reserved.
// Borrowed from https://github.com/gitextensions/gitextensions

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.Threading;

namespace DarcUI
{
    public static class ThreadHelper
    {
#pragma warning disable SA1139 // Use literal suffix notation instead of casting
        private const int RPC_E_WRONG_THREAD = unchecked((int)0x8001010E);
#pragma warning restore SA1139 // Use literal suffix notation instead of casting

        private static JoinableTaskContext s_joinableTaskContext = null!;
        private static JoinableTaskCollection s_joinableTaskCollection = null!;
        private static JoinableTaskFactory s_joinableTaskFactory = null!;

        public static JoinableTaskContext JoinableTaskContext
        {
            get
            {
                return s_joinableTaskContext;
            }

            internal set
            {
                if (value == s_joinableTaskContext)
                {
                    return;
                }

                if (value is null)
                {
                    s_joinableTaskContext = null!;
                    s_joinableTaskCollection = null!;
                    s_joinableTaskFactory = null!;
                }
                else
                {
                    s_joinableTaskContext = value;
                    s_joinableTaskCollection = value.CreateCollection();
                    s_joinableTaskFactory = value.CreateFactory(s_joinableTaskCollection);
                }
            }
        }

        public static JoinableTaskFactory JoinableTaskFactory => s_joinableTaskFactory;

        public static void ThrowIfNotOnUIThread([CallerMemberName] string callerMemberName = "")
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            if (!JoinableTaskContext.IsOnMainThread)
            {
                string message = string.Format(CultureInfo.CurrentCulture, "{0} must be called on the UI thread.", callerMemberName);
                throw new COMException(message, RPC_E_WRONG_THREAD);
            }
        }

        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public static void AssertOnUIThread()
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            Debug.Assert(JoinableTaskContext.IsOnMainThread, "Must be on the UI thread.");
        }

        public static void ThrowIfOnUIThread([CallerMemberName] string callerMemberName = "")
        {
            if (JoinableTaskContext.IsOnMainThread)
            {
                string message = string.Format(CultureInfo.CurrentCulture, "{0} must be called on a background thread.", callerMemberName);
                throw new COMException(message, RPC_E_WRONG_THREAD);
            }
        }

        public static void FileAndForget(this JoinableTask joinableTask, Func<Exception, bool>? fileOnlyIf = null)
        {
            joinableTask.Task.FileAndForget(fileOnlyIf);
        }

        public static void FileAndForget(this Task task, Func<Exception, bool>? fileOnlyIf = null)
        {
            JoinableTaskFactory.RunAsync(
                async () =>
                {
                    try
                    {
                        await task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Do not rethrow these
                    }
                    catch (Exception ex) when (fileOnlyIf?.Invoke(ex) ?? true)
                    {
                        await JoinableTaskFactory.SwitchToMainThreadAsync();
                        Application.OnThreadException(ex.Demystify());
                    }
                });
        }

        public static async Task JoinPendingOperationsAsync(CancellationToken cancellationToken)
        {
            await s_joinableTaskCollection.JoinTillEmptyAsync(cancellationToken);
        }

        public static T CompletedResult<T>(this Task<T> task)
        {
            if (!task.IsCompleted)
            {
                throw new InvalidOperationException();
            }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            return task.Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }

        public static T? CompletedOrDefault<T>(this Task<T> task)
        {
            if (!task.IsCompleted)
            {
                return default;
            }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
            return task.Result;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
        }
    }
}
