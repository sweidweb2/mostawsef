# Rolling out the patient details / orders filter changes

## Order of deployment

1. **Apply the migration** `20261114130000_OrderPatientStatusIndex` by hand, on a copy first. It builds
   `IX_orders_PatientId_Status` with `CREATE INDEX CONCURRENTLY` (outside a transaction, so orders can still be written)
   and then drops `IX_orders_PatientId`. The statements use `IF [NOT] EXISTS`, so a re-run after an interrupted build is
   safe. The API also runs it at startup, so a slow or failing migration blocks every request: do it before the deploy.
2. **Deploy the API.**
3. **Then ship the desktop.** An old API ignores the new `status` and `patientId` query parameters, so a new desktop
   against it would show every order in the system on each patient's page.

A new API with an old desktop is safe (extra JSON fields are ignored). The old desktop still fills the patient edit form
from the list row, which has no notes, so saving wipes them until it is updated.

## API changes

| Endpoint | Change |
| --- | --- |
| `GET /api/orders` | Optional `status` (Pending, PartiallyPaid, Paid, Cancelled, Refunded; 400 `invalid` otherwise) and `patientId`. |
| `GET /api/patients` | Optional `includeUnpaid=true` fills `hasUnpaidOrders` on each row (Pending or PartiallyPaid order); null otherwise. |
| `DELETE /api/patients/{id}` | 409 `patient_has_orders` while the patient has any order. Logged as `patients.delete_refused`. |
| `POST /api/orders/{id}/payments` | Answers from the loaded order (no second read). |

## To check after deploying

- `pg_stat_user_indexes`: `IX_orders_PatientSearchName` shows `idx_scan = 0` (nothing queries it); drop it if so.
- `dotnet ef migrations has-pending-model-changes` reports nothing (the snapshot was edited by hand).
