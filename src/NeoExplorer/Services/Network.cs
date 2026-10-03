using System.ComponentModel;
using System.Runtime.InteropServices;
using NeoExplorer.Core;

namespace NeoExplorer.Services;

/// <summary>
/// A computer on the network ("\\SERVER") or one of its shared folders ("\\SERVER\Share").
/// </summary>
public record NetworkItem(string Name, string Path);

public static class Network
{
    /// <summary>
    /// The shell's Network folder, for its icon.
    /// </summary>
    public const string ShellParsingName = "::{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";

    private const string NetworkFolder = "shell:" + ShellParsingName;

    private const int MAX_PREFERRED_LENGTH = -1;
    private const int NERR_Success = 0;
    private const uint STYPE_MASK = 0xFF;
    private const uint STYPE_DISKTREE = 0;
    private const uint STYPE_SPECIAL = 0x80000000;

    /// <summary>
    /// The computers File Explorer shows under Network, found through network discovery. Other devices, such as
    /// media players and routers, are left out since they have no shared folders. Each computer is reported to
    /// <paramref name="found"/> as soon as it is found, usually within a few seconds, but the task only completes
    /// when discovery gives up waiting for more, which can take half a minute. Runs on its own thread.
    /// </summary>
    public static Task<IReadOnlyList<NetworkItem>> GetComputersAsync(IProgress<NetworkItem>? found = null) => RunOnStaThreadAsync<IReadOnlyList<NetworkItem>>(() =>
    {
        var computers = new List<NetworkItem>();
        try
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;

            // Enumerate rather than asking for Count, which waits for discovery to finish.
            foreach (dynamic item in (System.Collections.IEnumerable)shell.NameSpace(NetworkFolder).Items())
            {
                string path = item.Path;
                if (PathParser.IsNetworkComputer(path) && !computers.Exists(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase)))
                {
                    var computer = new NetworkItem(item.Name, path);
                    computers.Add(computer);
                    found?.Report(computer);
                }
            }
        }
        catch (COMException)
        {
            // Network discovery is unavailable; show no computers.
        }

        return Sort(computers);
    });

    /// <summary>
    /// The shared folders of a computer, without hidden ones like C$ and shared printers.
    /// Throws <see cref="Win32Exception"/> if the computer can't be reached or doesn't allow listing its shares.
    /// This is blocking; call it from a background thread.
    /// </summary>
    public static IReadOnlyList<NetworkItem> GetShares(string computer)
    {
        int resume = 0;
        int result = NetShareEnum(computer, 1, out nint buffer, MAX_PREFERRED_LENGTH, out int count, out _, ref resume);
        try
        {
            if (result != NERR_Success)
            {
                throw new Win32Exception(result);
            }

            var shares = new List<NetworkItem>();
            int size = Marshal.SizeOf<SHARE_INFO_1>();
            for (int i = 0; i < count; i++)
            {
                var share = Marshal.PtrToStructure<SHARE_INFO_1>(buffer + i * size);
                if ((share.shi1_type & STYPE_MASK) == STYPE_DISKTREE
                    && (share.shi1_type & STYPE_SPECIAL) == 0
                    && !share.shi1_netname.EndsWith('$'))
                {
                    shares.Add(new NetworkItem(share.shi1_netname, System.IO.Path.Combine(computer, share.shi1_netname)));
                }
            }

            return Sort(shares);
        }
        finally
        {
            if (buffer != 0)
            {
                NetApiBufferFree(buffer);
            }
        }
    }

    private static List<NetworkItem> Sort(List<NetworkItem> items)
    {
        items.Sort((x, y) => ItemComparer.CompareNames(x.Name, y.Name));
        return items;
    }

    // Shell.Application is an apartment-threaded COM object, and discovery can block for a long time.
    private static Task<T> RunOnStaThreadAsync<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHARE_INFO_1
    {
        public string shi1_netname;
        public uint shi1_type;
        public string shi1_remark;
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetShareEnum(string servername, int level, out nint bufptr, int prefmaxlen, out int entriesread, out int totalentries, ref int resume_handle);

    [DllImport("netapi32.dll")]
    private static extern int NetApiBufferFree(nint buffer);
}
