namespace AppServerApi.Models.auth;

public record RegisterResponse(
    string AccessToken,
    string RefreshToken,
    UserPublic User
);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    UserPublic User
);

public record RefreshTokenRequest(
    string RefreshToken
);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken
);
