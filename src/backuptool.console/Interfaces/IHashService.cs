namespace BackupTool.Interfaces
{
    public interface IHashService
    {
        string CalculateHash(byte[] data);
    }
}
