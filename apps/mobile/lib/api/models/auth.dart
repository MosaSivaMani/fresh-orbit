// TEMPORARY: replace with fresh_api generated models after Phase 3
//
// docs/api/README.md §4.1: POST /auth/otp/request, /auth/otp/verify, /auth/refresh.

import 'json.dart';

class OtpChallenge {
  const OtpChallenge({
    required this.challengeId,
    required this.expiresAt,
    required this.resendAfterSeconds,
    required this.codeLength,
  });

  factory OtpChallenge.fromJson(Json j) => OtpChallenge(
        challengeId: reqString(j, 'challengeId'),
        expiresAt: reqInstant(j, 'expiresAt'),
        resendAfterSeconds: reqInt(j, 'resendAfterSeconds'),
        codeLength: reqInt(j, 'codeLength'),
      );

  final String challengeId;
  final DateTime expiresAt;
  final int resendAfterSeconds;
  final int codeLength;
}

/// Access + refresh token pair (verify and refresh responses).
class TokenPair {
  const TokenPair({
    required this.accessToken,
    required this.accessTokenExpiresAt,
    required this.refreshToken,
    required this.refreshTokenExpiresAt,
  });

  factory TokenPair.fromJson(Json j) => TokenPair(
        accessToken: reqString(j, 'accessToken'),
        accessTokenExpiresAt: reqInstant(j, 'accessTokenExpiresAt'),
        refreshToken: reqString(j, 'refreshToken'),
        refreshTokenExpiresAt: reqInstant(j, 'refreshTokenExpiresAt'),
      );

  final String accessToken;
  final DateTime accessTokenExpiresAt;
  final String refreshToken;
  final DateTime refreshTokenExpiresAt;
}

class AuthUser {
  const AuthUser({required this.id, required this.phone, this.fullName, required this.isNewCustomer});

  factory AuthUser.fromJson(Json j) => AuthUser(
        id: reqString(j, 'id'),
        phone: optString(j, 'phone'),
        fullName: optString(j, 'fullName'),
        isNewCustomer: optBool(j, 'isNewCustomer') ?? false,
      );

  final String id;
  final String? phone;
  final String? fullName;
  final bool isNewCustomer;
}

class VerifyOtpResult {
  const VerifyOtpResult({required this.tokens, required this.user});

  factory VerifyOtpResult.fromJson(Json j) =>
      VerifyOtpResult(tokens: TokenPair.fromJson(j), user: AuthUser.fromJson(asJson(j['user'], 'user')));

  final TokenPair tokens;
  final AuthUser user;
}
