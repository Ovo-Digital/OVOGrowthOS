namespace OvoGrowthOS.Api;

public static class StartupGuard
{
    public const string DefaultJwtKey = "local-development-key-change-before-production-32chars";
    public const string DefaultAdminPasswordHash = "b3ZvLWdyb3d0aC1vcw==.xSggXm+nchd1EWn9DGC+BcTBP9eIrh4WR+PBP46wGcA=";
    private const int MinJwtKeyLength = 32;

    public static void Ensure(IConfiguration configuration, string environmentName)
    {
        if (environmentName is "Development" or "Testing") return;
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Jwt:Key boş olamaz. Ortam değişkeni JWT_KEY değerini tanımlayın.");
        if (key == DefaultJwtKey)
            throw new InvalidOperationException($"Jwt:Key repodaki varsayılan değerde kalamaz. Üretim ortamı için {MinJwtKeyLength} karakterden uzun rastgele bir anahtar üretip JWT_KEY ortam değişkeniyle verin.");
        if (key.Length < MinJwtKeyLength)
            throw new InvalidOperationException($"Jwt:Key en az {MinJwtKeyLength} karakter olmalıdır.");
        var adminHash = configuration["DefaultAdmin:PasswordHash"];
        if (adminHash == DefaultAdminPasswordHash)
            throw new InvalidOperationException("DefaultAdmin:PasswordHash repodaki varsayılan parola özetiyle aynı. DEFAULT_ADMIN_PASSWORD_HASH ortam değişkeniyle kendi özetinizi verin; yönetici hesabı daha önce oluşturulduysa bu değer kullanılmaz, yine de yayında tutulmamalıdır.");
    }

    public static bool UsesDefaultAdminHash(IConfiguration configuration) =>
        configuration["DefaultAdmin:PasswordHash"] == DefaultAdminPasswordHash;
}
