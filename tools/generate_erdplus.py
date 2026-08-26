"""Generate an ERDPlus conceptual diagram aligned to EasyRent models."""
from __future__ import annotations

import json
import math
import time
import uuid
from pathlib import Path


def uid() -> str:
    return str(uuid.uuid4())


def entity(eid: str, label: str, x: float, y: float) -> dict:
    return {
        "id": eid,
        "type": "Entity",
        "position": {"x": x, "y": y},
        "data": {
            "label": label,
            "type": "Regular",
            "isConnectable": False,
            "isSelected": False,
        },
        "measured": {"width": 100, "height": 50},
        "selected": False,
        "dragging": False,
    }


def attribute(parent_id: str, label: str, x: float, y: float, pk: bool = False) -> dict:
    return {
        "id": uid(),
        "parentId": parent_id,
        "type": "Attribute",
        "position": {"x": x, "y": y},
        "data": {
            "label": label,
            "types": {"Unique": True} if pk else [],
            "isSelected": False,
            "isConnectable": False,
        },
        "measured": {"width": 100, "height": 50},
        "selected": False,
        "dragging": False,
    }


def place_attrs(parent_id: str, names: list[str], radius: float = 165) -> list[dict]:
    nodes = []
    count = len(names)
    for i, name in enumerate(names):
        angle = (2 * math.pi * i / count) - (math.pi / 2)
        pk = i == 0
        nodes.append(
            attribute(
                parent_id,
                name,
                math.cos(angle) * radius,
                math.sin(angle) * radius,
                pk=pk,
            )
        )
    return nodes


def relationship(
    rid: str,
    label: str,
    x: float,
    y: float,
    source_id: str,
    source_min: str,
    source_max: str,
    target_id: str,
    target_min: str,
    target_max: str,
) -> dict:
    return {
        "id": rid,
        "type": "Relationship",
        "position": {"x": x, "y": y},
        "data": {
            "label": label,
            "sourceEntityDetails": {
                "id": source_id,
                "minCardinality": source_min,
                "maxCardinality": source_max,
            },
            "targetEntityDetails": {
                "id": target_id,
                "minCardinality": target_min,
                "maxCardinality": target_max,
            },
            "isSelected": False,
            "isConnectable": True,
        },
        "measured": {"width": 100, "height": 50},
        "selected": False,
        "dragging": False,
    }


def attr_edge(source: str, target: str) -> dict:
    return {"id": uid(), "type": "Attribute", "source": source, "target": target}


def rel_edge(rel_id: str, entity_id: str, other_id: str, min_c: str, max_c: str) -> dict:
    return {
        "id": f"{rel_id}->{entity_id};{entity_id}->{other_id}",
        "type": "Relationship",
        "source": rel_id,
        "target": entity_id,
        "data": {
            "id": f"{rel_id}->{entity_id}",
            "minCardinality": min_c,
            "maxCardinality": max_c,
        },
    }


# Entity IDs (stable so the file is easier to inspect)
E = {
    "User": "db952d47-bbbf-474a-9734-ed669a4da94f",
    "CustomerProfile": "77fd252b-15de-4daf-a959-5d27c74e991f",
    "AdminProfile": "8a485fa5-5ae7-4597-b79f-faa66e259569",
    "Rental": "62d714a4-bb70-4156-86f6-4b35442754e9",
    "RentalDetails": "f7b115c9-d1cd-4c5c-a0c0-109c4e58b8d9",
    "Payment": "2b3cfab1-e0b2-4bbf-89d1-32053eab64be",
    "Vehicle": "d8d650ad-196e-470b-b9b3-a8619a2a3ed3",
    "Driver": "2e175208-e076-4d55-99bb-5a16dec5dc8f",
    "Transit": "9b540040-6a85-4bb5-9769-21c541683b88",
    "Feedback": "a1c4e8f2-0b31-4d77-9e5a-2c8f6d14b907",
    "MaintenancePlan": "c3e9a710-6d52-4b18-8f44-1a0e7c29d583",
    "MaintenanceLog": "787da44e-cd74-44c2-9461-b4c8d1e5a6b3",
    "IncidentReport": "77ea861b-00fa-4076-a93e-60cfdda7192f",
}

# Layout: accounts left, booking center, fleet/ops right
positions = {
    "User": (80, -40),
    "CustomerProfile": (80, 260),
    "AdminProfile": (80, -320),
    "Rental": (620, 80),
    "Payment": (620, 420),
    "RentalDetails": (980, -40),
    "Transit": (980, 360),
    "Feedback": (620, 700),
    "Vehicle": (1420, 80),
    "Driver": (1420, 520),
    "MaintenancePlan": (1860, -200),
    "MaintenanceLog": (1860, 140),
    "IncidentReport": (1860, 560),
}

# Persisted attributes only. First name is the PK. No FKs, no NotMapped uploads.
attributes = {
    "User": [
        "UserId(PK)",
        "Email",
        "PasswordHash",
        "Role",
        "CreatedAt",
        "EmailConfirmed",
        "EmailVerificationToken",
        "EmailVerificationTokenExpires",
        "PasswordResetToken",
        "PasswordResetTokenExpires",
        "AccessFailedCount",
        "LockoutEndUtc",
    ],
    "CustomerProfile": [
        "CustomerId(PK)",
        "FullName",
        "ContactNumber",
        "ValidIDtype",
        "ValidIDImagePath",
        "Status",
    ],
    "AdminProfile": [
        "AdminId(PK)",
        "FullName",
    ],
    "Rental": [
        "RentalId(PK)",
        "CustomerName",
        "ContactNumber",
        "Notes",
        "RentalStatus",
        "RentalOption",
        "PaymentDueAt",
        "CancelledAt",
        "CancellationFee",
    ],
    "RentalDetails": [
        "RentalDetailsID(PK)",
        "PickupLocation",
        "DropoffLocation",
        "PickupDate",
        "ReturnDate",
        "PickupTime",
        "ReturnTime",
        "PassengerCount",
        "Discount",
        "DiscountImagePath",
    ],
    "Payment": [
        "PaymentId(PK)",
        "PaymentMethod",
        "PaymentType",
        "TotalAmount",
        "AmountPaid",
        "AccountName",
        "TransactionReference",
        "PaymentDate",
        "ReceiptImagePath",
        "PaymentNotes",
    ],
    "Vehicle": [
        "VehicleId(PK)",
        "Model",
        "PlateNumber",
        "Brand",
        "Color",
        "Type",
        "Status",
        "Odometer",
        "RegistrationDate",
        "RegistrationExpiry",
        "BasePrice",
        "SucceedingFee",
        "PassengersCount",
        "Description",
        "ImagePath",
    ],
    "Driver": [
        "DriverId(PK)",
        "Name",
        "Address",
        "ContactNo",
        "LicenseNo",
        "ExpiryDate",
        "IsActive",
        "CreatedAt",
        "ImagePath",
        "FrontLicenseImagePath",
        "BackLicenseImagePath",
    ],
    "Transit": [
        "TransitID(PK)",
        "DepartureTime",
        "ReturnTime",
        "FuelLevelStart",
        "FuelLevelEnd",
        "OdometerStart",
        "OdometerEnd",
        "VehicleConditionStart",
        "VehicleConditionEnd",
        "PreTripImagePath",
        "PostTripImagePath",
        "Remarks",
        "TripStatus",
    ],
    "Feedback": [
        "FeedbackId(PK)",
        "Rating",
        "VehicleComfort",
        "VehiclePerformance",
        "VehicleSafety",
        "DriverProfessionalism",
        "DriverDriving",
        "DriverCourtesy",
        "Comment",
        "CreatedAt",
    ],
    "MaintenancePlan": [
        "MaintenancePlanId(PK)",
        "Type",
        "Trigger",
        "IntervalKilometers",
        "IntervalMonths",
        "LastCompletedDate",
        "LastOdometer",
        "NextDueDate",
        "NextDueOdometer",
    ],
    "MaintenanceLog": [
        "MaintenanceLogId(PK)",
        "Type",
        "Status",
        "ScheduledDate",
        "StartedAt",
        "CompletedAt",
        "Odometer",
        "Description",
        "WorkDone",
        "Cost",
        "ImagePath",
        "Remarks",
        "CreatedAt",
    ],
    "IncidentReport": [
        "IncidentReportId(PK)",
        "Type",
        "Severity",
        "Status",
        "OccurredAt",
        "Location",
        "Description",
        "Odometer",
        "IsUndrivable",
        "ImagePath",
        "RepairStartedAt",
        "WorkDone",
        "Cost",
        "Remarks",
        "CreatedAt",
        "ClosedAt",
    ],
}

# (label, source, smin, smax, target, tmin, tmax)
# Cardinality is read at each entity: how many of the other side attach to one of this entity? No —
# ERDPlus stores the participation of THAT entity in the relationship.
# Existing "operates": Vehicle Mandatory One, Transit Optional Many
# = each Transit participates exactly once; each Vehicle participates 0..N times.
rels_spec = [
    # User 1 —has— 0..1 CustomerProfile
    ("has", "User", "Optional", "One", "CustomerProfile", "Mandatory", "One"),
    # User 1 —has— 0..1 AdminProfile
    ("has", "User", "Optional", "One", "AdminProfile", "Mandatory", "One"),
    # Customer 1 —makes— 0..N Rentals (rental.CustomerId optional)
    ("makes", "CustomerProfile", "Optional", "Many", "Rental", "Optional", "One"),
    # Rental 1 —includes— 1 RentalDetails
    ("includes", "Rental", "Mandatory", "One", "RentalDetails", "Mandatory", "One"),
    # Rental 1 —requires— 0..N Payments
    ("requires", "Rental", "Optional", "Many", "Payment", "Mandatory", "One"),
    # RentalDetails N —rents— 1 Vehicle
    ("rents", "RentalDetails", "Mandatory", "One", "Vehicle", "Optional", "Many"),
    # Rental 1 —generates— 0..1 Transit
    ("generates", "Rental", "Optional", "One", "Transit", "Mandatory", "One"),
    # Vehicle 1 —operates— 0..N Transits
    ("operates", "Vehicle", "Optional", "Many", "Transit", "Mandatory", "One"),
    # Driver 0..1 —reports— 0..N Transits
    ("reports", "Driver", "Optional", "Many", "Transit", "Optional", "One"),
    # Transit 1 —receives— 0..1 Feedback
    ("receives", "Transit", "Optional", "One", "Feedback", "Mandatory", "One"),
    # Customer 1 —submits— 0..N Feedback
    ("submits", "CustomerProfile", "Optional", "Many", "Feedback", "Mandatory", "One"),
    # Vehicle 1 —schedules— 0..N MaintenancePlans
    ("schedules", "Vehicle", "Optional", "Many", "MaintenancePlan", "Mandatory", "One"),
    # Vehicle 1 —logs— 0..N MaintenanceLogs
    ("logs", "Vehicle", "Optional", "Many", "MaintenanceLog", "Mandatory", "One"),
    # Plan 1 —records— 0..N Logs (log.plan optional)
    ("records", "MaintenancePlan", "Optional", "Many", "MaintenanceLog", "Optional", "One"),
    # Vehicle 1 —has— 0..N Incidents
    ("has", "Vehicle", "Optional", "Many", "IncidentReport", "Mandatory", "One"),
    # Transit 0..1 —has— 0..N Incidents
    ("involves", "Transit", "Optional", "Many", "IncidentReport", "Optional", "One"),
    # Driver 0..1 —has— 0..N Incidents
    ("has", "Driver", "Optional", "Many", "IncidentReport", "Optional", "One"),
]


def midpoint(a: str, b: str) -> tuple[float, float]:
    ax, ay = positions[a]
    bx, by = positions[b]
    return ((ax + bx) / 2 + 20, (ay + by) / 2)


def main() -> None:
    nodes: list[dict] = []
    edges: list[dict] = []

    attr_nodes_by_entity: dict[str, list[dict]] = {}
    for name, eid in E.items():
        x, y = positions[name]
        nodes.append(entity(eid, name, x, y))
        radius = 155 if len(attributes[name]) <= 8 else 185 if len(attributes[name]) <= 12 else 215
        attrs = place_attrs(eid, attributes[name], radius=radius)
        attr_nodes_by_entity[name] = attrs
        nodes.extend(attrs)
        for a in attrs:
            edges.append(attr_edge(eid, a["id"]))

    for label, src, smin, smax, tgt, tmin, tmax in rels_spec:
        rid = uid()
        rx, ry = midpoint(src, tgt)
        nodes.append(
            relationship(
                rid, label, rx, ry,
                E[src], smin, smax,
                E[tgt], tmin, tmax,
            )
        )
        edges.append(rel_edge(rid, E[src], E[tgt], smin, smax))
        edges.append(rel_edge(rid, E[tgt], E[src], tmin, tmax))

    diagram = {
        "diagramType": 1,
        "data": {
            "nodes": nodes,
            "edges": edges,
            "viewport": {"x": 120, "y": 180, "zoom": 0.42},
        },
        "name": "CAPSTONE_EasyRent",
        "folder": {
            "name": "Diagrams",
            "folderType": 1,
            "depth": 0,
            "id": 189780,
        },
        "id": 503349,
        "updatedAtTimestamp": int(time.time()),
    }

    dest = Path(r"c:\Users\Lubi\Desktop\CAPSTONE_EasyRent.erdplus")
    dest.write_text(json.dumps(diagram, indent=2), encoding="utf-8")
    print(f"Wrote {dest}")
    print(f"Entities: {len(E)}  Nodes: {len(nodes)}  Edges: {len(edges)}  Rels: {len(rels_spec)}")


if __name__ == "__main__":
    main()
