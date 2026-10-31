using System;

namespace TecnoSpace3D
{
    public static class CameraMath3D
    {
        public static double ProcessAxis(int raw, double fullScale, double deadzone, double expo, double gain)
        {
            double norm = raw / fullScale;
            if (norm > 1.5) norm = 1.5;
            else if (norm < -1.5) norm = -1.5;

            double abs = Math.Abs(norm);
            if (abs <= deadzone) return 0.0;

            double rescaled = (abs - deadzone) / Math.Max(0.05, 1.0 - deadzone);
            double curved = (1.0 - expo) * rescaled + expo * (rescaled * rescaled * rescaled);
            return Math.Sign(norm) * curved * gain;
        }

        public static void ApplyDominant(ref double tx, ref double ty, ref double tz, ref double rx, ref double ry, ref double rz)
        {
            double[] v = new double[] { tx, ty, tz, rx, ry, rz };
            int maxIdx = 0;
            double maxVal = Math.Abs(v[0]);
            for (int i = 1; i < 6; i++)
            {
                double a = Math.Abs(v[i]);
                if (a > maxVal) { maxVal = a; maxIdx = i; }
            }
            tx = maxIdx == 0 ? tx : 0.0;
            ty = maxIdx == 1 ? ty : 0.0;
            tz = maxIdx == 2 ? tz : 0.0;
            rx = maxIdx == 3 ? rx : 0.0;
            ry = maxIdx == 4 ? ry : 0.0;
            rz = maxIdx == 5 ? rz : 0.0;
        }

        public static void OrbitAndPan(
            ref double eyeX, ref double eyeY, ref double eyeZ,
            ref double tgtX, ref double tgtY, ref double tgtZ,
            ref double upX, ref double upY, ref double upZ,
            double panWorldUnitsX, double panWorldUnitsY,
            double pitchRad, double yawRad, double rollRad)
        {
            double fx = tgtX - eyeX, fy = tgtY - eyeY, fz = tgtZ - eyeZ;
            double dist = Math.Sqrt(fx * fx + fy * fy + fz * fz);
            if (dist < 1e-7) return;
            fx /= dist; fy /= dist; fz /= dist;

            Normalize(ref upX, ref upY, ref upZ);

            // right = normalize(forward x up)
            double rx, ry, rz;
            Cross(fx, fy, fz, upX, upY, upZ, out rx, out ry, out rz);
            Normalize(ref rx, ref ry, ref rz);

            // trueUp = normalize(right x forward)
            Cross(rx, ry, rz, fx, fy, fz, out upX, out upY, out upZ);
            Normalize(ref upX, ref upY, ref upZ);

            // 1. Pan camera Eye & Target in screen plane
            if (panWorldUnitsX != 0.0 || panWorldUnitsY != 0.0)
            {
                double dx = -panWorldUnitsX * rx - panWorldUnitsY * upX;
                double dy = -panWorldUnitsX * ry - panWorldUnitsY * upY;
                double dz = -panWorldUnitsX * rz - panWorldUnitsY * upZ;
                eyeX += dx; eyeY += dy; eyeZ += dz;
                tgtX += dx; tgtY += dy; tgtZ += dz;
            }

            // Vector from target to eye
            double ox = eyeX - tgtX, oy = eyeY - tgtY, oz = eyeZ - tgtZ;

            // 2. Pitch around screen Right
            if (pitchRad != 0.0)
            {
                RotateAroundAxis(ref ox, ref oy, ref oz, rx, ry, rz, -pitchRad);
                RotateAroundAxis(ref upX, ref upY, ref upZ, rx, ry, rz, -pitchRad);
            }

            // 3. Yaw around screen Up
            if (yawRad != 0.0)
            {
                RotateAroundAxis(ref ox, ref oy, ref oz, upX, upY, upZ, -yawRad);
            }

            // 4. Roll around Forward
            if (rollRad != 0.0)
            {
                double nfx = -ox, nfy = -oy, nfz = -oz;
                Normalize(ref nfx, ref nfy, ref nfz);
                RotateAroundAxis(ref upX, ref upY, ref upZ, nfx, nfy, nfz, rollRad);
            }

            Normalize(ref upX, ref upY, ref upZ);
            eyeX = tgtX + ox;
            eyeY = tgtY + oy;
            eyeZ = tgtZ + oz;
        }

        public static void RotateAroundAxis(ref double vx, ref double vy, ref double vz, double ax, double ay, double az, double angle)
        {
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            double dot = vx * ax + vy * ay + vz * az;
            double cx, cy, cz;
            Cross(ax, ay, az, vx, vy, vz, out cx, out cy, out cz);

            vx = vx * c + cx * s + ax * dot * (1.0 - c);
            vy = vy * c + cy * s + ay * dot * (1.0 - c);
            vz = vz * c + cz * s + az * dot * (1.0 - c);
        }

        private static void Cross(double ax, double ay, double az, double bx, double by, double bz, out double cx, out double cy, out double cz)
        {
            cx = ay * bz - az * by;
            cy = az * bx - ax * bz;
            cz = ax * by - ay * bx;
        }

        private static void Normalize(ref double x, ref double y, ref double z)
        {
            double len = Math.Sqrt(x * x + y * y + z * z);
            if (len > 1e-9) { x /= len; y /= len; z /= len; }
        }
    }
}
