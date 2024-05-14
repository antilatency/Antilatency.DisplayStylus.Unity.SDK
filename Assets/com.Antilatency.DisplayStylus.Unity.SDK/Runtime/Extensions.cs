
namespace Antilatency.DisplayStylus {
    public static partial class Extensions {
        public static bool IsNull(this Antilatency.InterfaceContract.IUnsafe value) {
            if (value == null)
                return true;
            if (value is Antilatency.InterfaceContract.Details.IUnsafeWrapper wrapper) {
                return !wrapper;
            }

            return false;
        }
    }
}