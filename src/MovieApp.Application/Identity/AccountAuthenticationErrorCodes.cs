namespace MovieApp.Application.Identity;

public static class AccountAuthenticationErrorCodes
{
    public const string AccountExistsDifferentSignInMethod = "ACCOUNT_EXISTS_DIFFERENT_SIGN_IN_METHOD";

    public const string PasswordLoginRequiresVerifiedEmail = "PASSWORD_LOGIN_REQUIRES_VERIFIED_EMAIL";

    public const string FinalSignInMethodCannotBeRemoved = "FINAL_SIGN_IN_METHOD_CANNOT_BE_REMOVED";

    public const string ProviderAlreadyLinked = "PROVIDER_ALREADY_LINKED";

    public const string ProviderNotLinked = "PROVIDER_NOT_LINKED";

    public const string TargetProviderLinkedToAnotherUser = "TARGET_PROVIDER_LINKED_TO_ANOTHER_USER";
}
