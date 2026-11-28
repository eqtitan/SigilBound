// Compile-check stubs for KTX for Unity (com.unity.cloud.ktx 3.7.1, Apache-2.0): only what HeroModels uses.
// Signatures copied from the package's Runtime/Scripts (TextureBase.cs, KtxTexture.cs, TextureResult.cs, ErrorCode.cs).
using System;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;

namespace KtxUnity
{
    public enum ErrorCode { Success, UnsupportedVersion, UnsupportedFormat, UnsupportedPlatform, LoadingFailed, TranscodeFailed, InvalidState, FormatUnsupportedBySystem }

    public class TextureResult
    {
        public Texture2D texture;
        public ErrorCode errorCode = ErrorCode.Success;
    }

    public abstract class TextureBase
    {
        public Task<TextureResult> LoadFromBytes(NativeArray<byte>.ReadOnly data, bool linear = false, uint layer = 0, uint faceSlice = 0, uint mipLevel = 0, bool mipChain = true) => throw new NotImplementedException();
    }

    public class KtxTexture : TextureBase { }
}
