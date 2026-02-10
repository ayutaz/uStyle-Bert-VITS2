using System;
using System.Runtime.InteropServices;

namespace uStyleBertVITS2.Native
{
    /// <summary>
    /// OpenJTalk ネイティブハンドルの SafeHandle 実装。
    /// GC によるファイナライズ時もネイティブリソースを確実に解放する。
    /// </summary>
    public class OpenJTalkHandle : SafeHandle
    {
        public OpenJTalkHandle() : base(IntPtr.Zero, ownsHandle: true)
        {
        }

        public OpenJTalkHandle(IntPtr handle) : base(IntPtr.Zero, ownsHandle: true)
        {
            SetHandle(handle);
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                OpenJTalkNative.openjtalk_destroy(handle);
                return true;
            }
            return false;
        }
    }
}
