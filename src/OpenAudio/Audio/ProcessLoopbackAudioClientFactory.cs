using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenAudio.Services;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace OpenAudio.Audio;

internal static class ProcessLoopbackAudioClientFactory
{
    private const string VirtualAudioDeviceProcessLoopback = @"VAD\Process_Loopback";
    private static readonly Guid IidAudioClient = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

    public static AudioClient Create(uint processId, SessionLogger logger)
    {
        var activationParams = new AudioClientActivationParams
        {
            ActivationType = AudioClientActivationType.ProcessLoopback,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = processId,
                ProcessLoopbackMode = ProcessLoopbackMode.IncludeTargetProcessTree
            }
        };

        var activationParamsSize = Marshal.SizeOf<AudioClientActivationParams>();
        var activationParamsPointer = Marshal.AllocHGlobal(activationParamsSize);
        IActivateAudioInterfaceAsyncOperation? activationOperation = null;

        try
        {
            Marshal.StructureToPtr(activationParams, activationParamsPointer, false);

            var propVariant = new PropVariant
            {
                vt = (ushort)VarEnum.VT_BLOB,
                blob = new Blob
                {
                    cbSize = activationParamsSize,
                    pBlobData = activationParamsPointer
                }
            };

            var completionHandler = new ActivateAudioInterfaceCompletionHandler();
            var iid = IidAudioClient;
            var hr = NativeMethods.ActivateAudioInterfaceAsync(
                VirtualAudioDeviceProcessLoopback,
                ref iid,
                ref propVariant,
                completionHandler,
                out activationOperation);

            Marshal.ThrowExceptionForHR(hr);

            var activatedInterfacePointer = completionHandler.WaitForResult();
            try
            {
                var audioClientInterface = (IAudioClient)Marshal.GetTypedObjectForIUnknown(activatedInterfacePointer, typeof(IAudioClient));
                logger.Log($"Activated process loopback capture for PID {processId}.");
                return new AudioClient(audioClientInterface);
            }
            finally
            {
                if (activatedInterfacePointer != IntPtr.Zero)
                {
                    Marshal.Release(activatedInterfacePointer);
                }
            }
        }
        finally
        {
            if (activationOperation is not null)
            {
                Marshal.ReleaseComObject(activationOperation);
            }

            Marshal.FreeHGlobal(activationParamsPointer);
        }
    }

    private static class NativeMethods
    {
        [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = true)]
        public static extern int ActivateAudioInterfaceAsync(
            [MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            ref Guid riid,
            ref PropVariant activationParams,
            IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientActivationParams
    {
        public AudioClientActivationType ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    private enum AudioClientActivationType
    {
        Default = 0,
        ProcessLoopback = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public ProcessLoopbackMode ProcessLoopbackMode;
    }

    private enum ProcessLoopbackMode
    {
        IncludeTargetProcessTree = 0,
        ExcludeTargetProcessTree = 1
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public Blob blob;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int cbSize;
        public IntPtr pBlobData;
    }

    [ComImport]
    [Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceCompletionHandler
    {
        void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
    }

    [ComImport]
    [Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IActivateAudioInterfaceAsyncOperation
    {
        void GetActivateResult(out int activateResult, out IntPtr activatedInterface);
    }

    [ComImport]
    [Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAgileObject
    {
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ActivateAudioInterfaceCompletionHandler : IActivateAudioInterfaceCompletionHandler, IAgileObject
    {
        private readonly TaskCompletionSource<IntPtr> _taskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation)
        {
            try
            {
                activateOperation.GetActivateResult(out var activationHr, out var activatedInterface);
                Marshal.ThrowExceptionForHR(activationHr);

                _taskCompletionSource.TrySetResult(activatedInterface);
            }
            catch (Exception exception)
            {
                _taskCompletionSource.TrySetException(exception);
            }
        }

        public IntPtr WaitForResult() => _taskCompletionSource.Task.GetAwaiter().GetResult();
    }
}

