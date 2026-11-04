namespace Scheduler.Application.Exceptions;

public class VinOwnedByOtherCustomerException()
    : DomainException("VIN_OWNED_BY_OTHER_CUSTOMER", "This VIN is registered to a different customer.");
