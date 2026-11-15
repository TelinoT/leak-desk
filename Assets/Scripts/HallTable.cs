using System.Collections.Generic;
using UnityEngine;

public class HallTable : MonoBehaviour
{
    [SerializeField] private int tableNumber = 1;
    [SerializeField] private List<Transform> seats = new();

    private GuestAgent[] occupants;

    public int TableNumber => tableNumber;

    private GuestAgent[] Occupants => occupants ??= new GuestAgent[seats.Count];

    public int FreeSeats
    {
        get
        {
            int free = 0;
            foreach (var guest in Occupants)
                if (guest == null) free++;
            return free;
        }
    }

    public bool TryReserveSeat(GuestAgent guest, out Transform seat)
    {
        for (int i = 0; i < Occupants.Length; i++)
        {
            if (Occupants[i] == null)
            {
                Occupants[i] = guest;
                seat = seats[i];
                return true;
            }
        }
        seat = null;
        return false;
    }

    public void FreeSeat(GuestAgent guest)
    {
        for (int i = 0; i < Occupants.Length; i++)
            if (Occupants[i] == guest) Occupants[i] = null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        foreach (var seat in seats)
        {
            if (seat == null) continue;
            Gizmos.DrawWireSphere(seat.position, 0.25f);
            Gizmos.DrawRay(seat.position, seat.forward * 0.6f);
        }
    }
}