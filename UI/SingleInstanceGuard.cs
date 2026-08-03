using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MgaWwiseIMImporter.UI;

/// <summary>
/// 名前付き Mutex で二重起動を防ぐ。既に起動中なら既存ウィンドウを前面化する。
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    /// <summary>セッション内で一意。クラッシュ後の AbandonedMutex も WaitOne で引き継ぐ。</summary>
    private const string MutexName = @"Local\MGA.Wwise.IMImporter.SingleInstance";

    private const int SwRestore = 9;

    private readonly Mutex _mutex;
    private readonly bool _ownsMutex;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
    }

    /// <summary>
    /// 初回起動ならガードを返す。既に別インスタンスが動いていれば null（既存窓を前面化済み）。
    /// </summary>
    public static SingleInstanceGuard? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool owns;
        try
        {
            owns = mutex.WaitOne(TimeSpan.Zero, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            // 前回プロセスが異常終了した場合でも所有権を引き継ぐ。
            owns = true;
        }

        if (owns)
        {
            return new SingleInstanceGuard(mutex, ownsMutex: true);
        }

        mutex.Dispose();
        ActivateExistingInstance();
        return null;
    }

    public void Dispose()
    {
        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // 所有していない場合は無視。
            }
        }

        _mutex.Dispose();
    }

    private static void ActivateExistingInstance()
    {
        var current = Process.GetCurrentProcess();
        Process[] candidates;
        try
        {
            candidates = Process.GetProcessesByName(current.ProcessName);
        }
        catch
        {
            return;
        }

        foreach (var process in candidates)
        {
            try
            {
                if (process.Id == current.Id)
                {
                    continue;
                }

                var handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                _ = AllowSetForegroundWindow(process.Id);
                _ = ShowWindow(handle, SwRestore);
                _ = SetForegroundWindow(handle);
                return;
            }
            catch
            {
                // 終了直後のプロセスなどはスキップ。
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
