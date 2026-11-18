namespace ECommerce.Application.Features.Payments.Shared;

public enum ZarinPalVerificationOutcome
{
    Skipped = 0,
    Successful = 1,
    Failed = 2,
    Indeterminate = 3,
    RequiresManualReview = 4
}
