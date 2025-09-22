using BackupTool.Interfaces;
using BackupTool.Services;
using Moq;

namespace BackupServiceTests
{
    [TestClass]
    public class BackupServiceTestsBase
    {
        internal Mock<IUnitOfWork> _unitOfWork = null!;
        internal Mock<IFileSystemService> _fileSystem = null!;
        internal Mock<IHashService> _hashService = null!;
        internal Mock<Microsoft.Extensions.Logging.ILogger<BackupService>> _logger = null!;
        internal BackupService _service = null!;
        internal Mock<ISnapshotRepository> _snapshotRepository = null!;

        [TestInitialize]
        public void Setup()
        {
            _unitOfWork = new Mock<IUnitOfWork>();
            _fileSystem = new Mock<IFileSystemService>();
            _hashService = new Mock<IHashService>();
            _logger = new Mock<Microsoft.Extensions.Logging.ILogger<BackupService>>();
            _snapshotRepository = new Mock<ISnapshotRepository>();

            _service = new BackupService(_unitOfWork.Object, _hashService.Object, _fileSystem.Object, _logger.Object);

            _unitOfWork.Setup(x => x.Snapshots).Returns(_snapshotRepository.Object);
        }
    }
}