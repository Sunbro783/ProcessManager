using System;

namespace ProcessManager.Services
{
    public static class AffinityHelper
    {
        public static IntPtr CreateMask(bool[] cores)
        {
            long mask = 0;
            for (int i = 0; i < cores.Length; i++)
                if (cores[i]) mask |= (1L << i);
            return new IntPtr(mask);
        }

        public static bool IsCoreEnabled(IntPtr mask, int index)
        {
            return (mask.ToInt64() & (1L << index)) != 0;
        }
    }
}