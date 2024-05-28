using UnityEngine;

namespace Antilatency.DisplayStylus.SDK
{
    public static class Math 
    {
        public static Matrix4x4 QuaternionToMatrix(Quaternion q)
        {
            float w = q.w;
            float x = q.x;
            float y = q.y;
            float z = q.z;
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;
            Matrix4x4 matrix = new Matrix4x4();
            matrix.m00 = 1.0f - 2.0f * (yy + zz);
            matrix.m01 = 2.0f * (xy - wz);
            matrix.m02 = 2.0f * (xz + wy);
            matrix.m03 = 0.0f;
            matrix.m10 = 2.0f * (xy + wz);
            matrix.m11 = 1.0f - 2.0f * (xx + zz);
            matrix.m12 = 2.0f * (yz - wx);
            matrix.m13 = 0.0f;
            matrix.m20 = 2.0f * (xz - wy);
            matrix.m21 = 2.0f * (yz + wx);
            matrix.m22 = 1.0f - 2.0f * (xx + yy);
            matrix.m23 = 0.0f;
            matrix.m30 = 0.0f;
            matrix.m31 = 0.0f;
            matrix.m32 = 0.0f;
            matrix.m33 = 1.0f;
            return matrix;
        }
    }
}
