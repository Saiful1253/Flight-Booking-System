namespace FlightBookingSystem.Models;

public enum UserRole { Client = 1, Admin = 2 }

public enum CabinClass { Economy = 1, Business = 2, First = 3 }

public enum BookingStatus { Pending = 1, Confirmed = 2, Rejected = 3, Cancelled = 4 }

public enum FlightStatus { Scheduled = 1, Delayed = 2, Cancelled = 3, Departed = 4, Arrived = 5 }

public enum Gender { Male = 1, Female = 2, Other = 3 }

public enum IdType { NID = 1, Passport = 2, BirthCertificate = 3 }
