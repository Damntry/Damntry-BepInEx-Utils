using Mirror;

namespace Damntry.UtilsBepInEx.MirrorNetwork {
    public static class MirrorUtils {

        /// <param name="byteOffset">The position in the dirty bits where the var has its value.</param>
        /// <param name="keepUnmodified">
        /// Restores the read position so it looks as if we didnt read from it.
        /// Otherwise the NetworkReader will desync when used as if its used expecting it to be untouched,
        /// like when used in a NetworkBehaviour.DeserializeSyncVars call.
        /// </param>
        public static bool IsSyncVarDirty(NetworkReader reader, long byteOffset, bool keepUnmodified) {
            int pos = reader.Position;
            long dirtyBits = (long)reader.ReadULong();

            if (keepUnmodified) {
                reader.Position = pos;
            }

            return (dirtyBits & byteOffset) != 0L;
        }

    }
}
