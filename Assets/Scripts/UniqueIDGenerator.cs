public static class UniqueIDGenerator
{
    private static int id = int.MinValue;

    public static int GetId()
    {
        int newID = id;
        id += 1;

        return newID;
    }
}