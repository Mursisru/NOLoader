namespace NOLoader.API
{
    public interface INOModArrayPool
    {
        int[] RentInt(int length);
        float[] RentFloat(int length);
        void Return(int[] array);
        void Return(float[] array);
    }
}
