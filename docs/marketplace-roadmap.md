# Customer marketplace roadmap

The goal is a Booksy-like customer side next to the studio back office:
- Customers see studios near them on a map, with ratings and the kinds of work they do.
- They pick services, get an approximate price for their car and send a booking request, which the studio accepts.
- They can keep an account with their cars and past bookings, and rate a studio after a visit.

Each step ships on its own. The studio confirms each request, so the customer only suggests a time; a studio that
plans (bays and opening hours, see "Planning" in `api-conventions.md`) shows its free times on its page, keeps
requests inside its hours and gets a planning view to answer them.

## 1. Service categories and prices per vehicle size (done)
- Services have a category from a fixed list (`Scheduling.Contracts.ServiceCategory`) and an optional price per
  vehicle size (`Shared.Kernel.Vehicles.VehicleSize`) in `scheduling.service_price`.
- CRM vehicles have a size.
- See "Services, categories and vehicle sizes" in `api-conventions.md`.

## 2. Public studio profile and map search (new Marketplace module) (done)
- Module `Modules/Marketplace`, schema `marketplace`. Add it to `ModuleBoundaryTests.Modules`, the Dockerfile and the solution.
- `StudioListing`, one per studio. The owner writes only its description and `IsPublished` (off until the owner
  publishes) through a staff endpoint.
- The studio's name, phone, address, city and pin (`Latitude`/`Longitude`, placed by the owner; no geocoding service)
  are Identity's, edited with `PUT /api/studios/current`. The listing keeps a copy: a new listing pulls it through
  `IStudioDirectory.FindDetailsAsync`, and `StudioDetailsChanged` keeps it current (`ListingStudioDetailsConsumer`).
  So a studio has one name everywhere. Publishing needs an address, city and pin; a listing that loses them is unpublished.
- Marketplace keeps its own copy of each studio's services, categories and prices. A new Scheduling event,
  `ServiceCatalogChanged`, feeds it from the outbox, the same way `BookingCopiesConsumer` keeps booking copies.
  A new listing pulls the current catalogue through `Scheduling.Contracts.IServiceCatalogSource`, so existing
  studios need no backfill.
- Public, anonymous, rate-limited endpoints (`AddRateLimiter`):
  - `GET /api/public/studios?minLat&maxLat&minLng&maxLng&category&vehicleSize&page` searches the visible map area.
    It uses a lat/lng index (no PostGIS yet) and returns the pin, name, rating, categories and the "from" price for
    the chosen size.
  - `GET /api/public/studios/{id}` returns the profile, services with prices and recent reviews.
- Listings are public by design, so they are not `ITenantEntity`. Owner edits filter by the caller's studio explicitly.

## 3. Customer accounts and the garage (new Customers module) (done)
- Customers sign up with Supabase from the frontend, and the `handle_new_user` trigger creates their profile row.
  A customer is a user with no studio membership; one person can be both.
- `CustomerProfile(UserId, Name, Phone)` and `CustomerVehicle(Id, UserId, LicensePlate, Brand, Model, VehicleSize)`.
- `/api/me/profile` and `/api/me/vehicles` are `[AllowWithoutStudio]` and scoped to the caller. The saved cars fill
  in booking requests.
- Rows are per account: an `Owner` query filter (like the tenant filter, failing closed) limits them to the
  signed-in user. At most ten cars, one per plate. The plate, phone and size rules are shared with CRM
  (`Shared.Application.Validation`).

## 4. Booking requests and booking history (Scheduling) (done)
- `BookingRequest` holds:
  - the studio and the customer user;
  - a snapshot of the customer and car (name, phone, email, plate, brand, model, size);
  - the services, a preferred time (UTC, shown in the studio's zone) and notes;
  - the estimated price and a status (`Pending`, `Accepted`, `Rejected`, `Withdrawn`).
- Customer endpoints:
  - `POST /api/me/booking-requests` sends a request;
  - `POST /api/public/studios/{id}/estimate` previews the price without logging in.
  - The server always recomputes the estimate from the per-size prices. A service without a price for that size
    shows as "price on request".
- Staff endpoints:
  - `GET /api/booking-requests` lists requests, pending first;
  - `POST .../{id}/accept` takes an optional final price and time;
  - `POST .../{id}/reject` takes a reason.
- On acceptance:
  - A new CRM contract method, `IClientDirectory.EnsureOnlineCustomerAsync`, finds or creates the studio's client (linked
    by customer user ID) and vehicle (by plate). It is idempotent.
  - A normal `Booking` is then created with a nullable `CustomerUserId`, so the visit shows in the calendar.
- `GET /api/me/bookings` returns the customer's requests and bookings across studios. It bypasses only the `Tenant`
  filter, by name, and filters by the caller.
- Emails go through the outbox:
  - a new request goes to the studio's staff (via `IStudioDirectory`);
  - accepted and rejected go to the customer, with times in the studio's zone.

## 5. Reviews (Marketplace) (done)
- Scheduling publishes `BookingCompleted(StudioId, BookingId, CustomerUserId?, VehicleSize, ServiceIds, CompletedAt)`
  (Inventory uses it for materials). Marketplace's `ReviewableVisitConsumer` records a `ReviewableVisit` for visits
  booked from a customer account, keyed by booking, so redelivery is harmless.
- `POST /api/me/reviews` takes `{ bookingId, rating 1–5, comment }`: once per completed booking, only by that customer,
  within 90 days. The author shows as first name and initial.
- The average rating and review count are computed from the reviews when read (indexed by studio), so they are never
  out of step and concurrent reviews can't lose an update. Reviews are shown paged on the studio page.

## 6. Chat between customers and studios (new Chat module) (done)
Design and decisions: `docs/chat-module-plan.md`.
- One conversation per studio and customer account. A customer opens one with any published studio
  (`POST /api/me/conversations { studioId }`); a studio only with someone who sent it a booking request
  (`POST /api/conversations { bookingRequestId }`). The studio's side is a shared inbox for all its active staff.
- Both sides list conversations with unread counts, read a total for the badge (`unread-count`), page messages
  newest first by cursor, send (`{ body, clientMessageId }`, idempotent per client message id, at most
  `RateLimiting:ChatPermitLimit` a minute per user) and mark read up to a message they have seen.
- Messages and read receipts are pushed over SignalR at `/hubs/chat` (`MessageReceived`, `ConversationRead`). The hub
  only listens; staff call `WatchStudio(studioId)` for each inbox they show. A single API instance, no backplane yet.
- The studio's name in conversations follows `StudioDetailsChanged`; the customer's name is copied when opening.

## Later
Instant booking (a free slot booked without confirmation), staff schedules and skills, photos (Supabase Storage), PostGIS "nearest first", studio replies to
reviews, SMS.
