using Microsoft.SemanticKernel.Connectors.SqliteVec;
using NineKgTools.Core.Models.Vectors;
using NineKgTools.Core.Services.Configs;
using Serilog;

namespace NineKgTools.Core.Services.Vectors;

/// <summary>
/// SQLite 向量数据库服务（Singleton，线程安全）
/// </summary>
public partial class VectorService
{
    private readonly VectorDbConfig _config;
    private readonly SqliteVectorStore _vectorStore;

    // 具体的集合实例
    private SqliteCollection<string, TagVector>? _tagCollection;
    private SqliteCollection<string, MediaVector>? _mediaCollection;

    // 写操作信号量：SQLite 不支持并发写入，需串行化所有写操作
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // 初始化信号量：保证 InitializeAsync 线程安全
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public VectorService(VectorDbConfig config)
    {
        _config = config;

        try
        {
            // 确保数据库文件路径是绝对路径
            var connectionString = EnsureAbsolutePath(_config.ConnectionString);

            // 确保数据库目录存在
            EnsureDatabaseDirectoryExists(connectionString);

            // 创建 SQLite 向量存储
            _vectorStore = new SqliteVectorStore(connectionString);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "初始化 SQLite 向量数据库失败");
            throw;
        }
    }

    /// <summary>
    /// 初始化数据库（创建所有必要的集合）
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // 双重检查锁定
        if (_isInitialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized) return;

            // 初始化标签集合
            _tagCollection = _vectorStore.GetCollection<string, TagVector>("Tags");
            await _tagCollection.EnsureCollectionExistsAsync(cancellationToken);

            // 初始化媒体集合
            _mediaCollection = _vectorStore.GetCollection<string, MediaVector>("Media");
            await _mediaCollection.EnsureCollectionExistsAsync(cancellationToken);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "向量数据库初始化失败");
            throw;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 测试连接
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_tagCollection == null)
            {
                await InitializeAsync(cancellationToken);
            }

            var exists = await _tagCollection!.CollectionExistsAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Log.Error(ex, "测试向量数据库连接失败");
            }
            return false;
        }
    }

    private string GenerateId(string prefix)
    {
        return $"{prefix.ToLower()}_{Guid.NewGuid():N}";
    }

    private string EnsureAbsolutePath(string connectionString)
    {
        var dataSourcePattern = @"Data Source=([^;]+)";
        var match = System.Text.RegularExpressions.Regex.Match(connectionString, dataSourcePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        if (match.Success && match.Groups.Count > 1)
        {
            var dbPath = match.Groups[1].Value.Trim().Trim('"', '\'');
            
            // 跳过内存数据库
            if (dbPath == ":memory:")
            {
                return connectionString;
            }
            
            // 如果不是绝对路径，转换为绝对路径
            if (!Path.IsPathRooted(dbPath))
            {
                var absolutePath = Path.GetFullPath(dbPath);
                var newConnectionString = connectionString.Replace(match.Groups[1].Value, absolutePath);
                return newConnectionString;
            }
        }
        
        return connectionString;
    }
    
    private void EnsureDatabaseDirectoryExists(string connectionString)
    {
        try
        {
            var dataSourcePattern = @"Data Source=([^;]+)";
            var match = System.Text.RegularExpressions.Regex.Match(connectionString, dataSourcePattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (match.Success && match.Groups.Count > 1)
            {
                var dbPath = match.Groups[1].Value.Trim().Trim('"', '\'');
                
                // 跳过内存数据库
                if (dbPath == ":memory:")
                {
                    return;
                }
                
                // 获取目录路径
                var directory = Path.GetDirectoryName(dbPath);
                
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Log.Information("创建向量数据库目录: {Directory}", directory);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建数据库目录时出错");
            throw;
        }
    }
}