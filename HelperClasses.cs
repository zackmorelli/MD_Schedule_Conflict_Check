using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MD_CarePath_Schedule_Conflict_Check
{
    class MDCarePath
    {
        public long ActIntSer { get; set; }

        public long PatSer { get; set; }

        public string PatName { get; set; }

        public DateTime DueDate { get; set; }

        public string Status { get; set; }
        // this is the NonScheduledActivityCode, which is really the status of the NonScheduledActivity (or Task), like "Open", "Completed", etc. In case we need it.

        public long ActSer { get; set; }

        public long ResourceSer { get; set; }
        // ResourceSer of the doctor associated with this carepath

        public override string ToString()
        {
            return "PatName: " + PatName + "  PatSer: " + PatSer + "  Doc: " + ResourceSer + "  Due Date: " + DueDate + "  Status: " + Status;
        }
    }


    class MDAppointment
    {

        public long SchedActSer { get; set; }

        public long ResourceSer { get; set; }

        public long ActIntSer { get; set; }

        public long ActSer { get; set; }

        public string ActCode { get; set; }
        // This is actually the activity type, like "Consult", "Follow-Up", "Daily Treatment", etc.
        // for this we are specifically looking for "Contract Off", a type of appointment (Scheduled Activity) used when the doctor is not working that day
        // And "Administration" a type of appointment used when the doctor is working at a non-Lahey Location (Not Burlington or Peabody).
        // and vacation.
        // "Administration" appointments will have an Activity Note that says "Not at this site". We need this beacuse IDK what other appointments use the "Administration" Type.
        // just a note, in the graphical schedule inteface in ARIA, you can only look at a calendar for one location or "department" at a time.
        // So, on the Burlington Schedule there will be an appointment with an Activity Code "See Peabody Schedule" for a doctor that is working in Peabody on that day.
        // and if you are looking at the Peabody schedule there will be an appointment with an Activity Code "See Burlington Schedule" for a Doctor that is working in Peabody on that day.
        // This program does not deal with that, at least right now. It just alerts you if a doctor has a "Contours Needed" or "Plan Review" task due when they are either contract off or not working within Lahey Proper.

        public string ActNote { get; set; }
        // This is the note, or description, that an appointment has on the graphical inteface. We need this for the "Not at this site" note, because I'm assuming their are other "Administration" Type appointments.

        public DateTime Start { get; set; }

        public DateTime Stop { get; set; }
        // Don't really need these because all of the "Contract Off" and "Administartion" appointments that we are looking for should be 9 to 5. But we'll include them.
        // the program's logic will work per day, so when we find these appointments, we know the doctor will be either Off or non-Lahey that day and any tasks they have that day will be flagged.
        // But we'll include the times because we need to query the Scheduled Activities table anyway.

        public override string ToString()
        {
            return "ActivityCode: " + ActCode + "  ActNote: " + ActNote + "  Doc: " + ResourceSer + "  Start Time: " + Start;
        }

    }

    class Conflict : IComparable<Conflict>
    {

        public string Doc { get; set; }

        public DateTime Time { get; set; }

        public string ConflictType { get; set; }

        public string Pat { get; set; }

        public string TaskType { get; set; }

        public override string ToString()
        {
            return Doc + " is " + ConflictType + " both on " + Time.ToShortDateString() + " AND the day before, but he/she has an open " + TaskType + " Care Path Task for Patient " + Pat + " due on " + Time.ToShortDateString() + "!";
        }

        public int CompareTo(Conflict comparecon)
        {
            if(comparecon == null)
            {
                return 1;
            }
            else
            {
                return this.Doc.CompareTo(comparecon.Doc);
            }
        }

        public bool Equals(Conflict eqcon)
        {
            if(this.ToString() == eqcon.ToString())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        // So this is called in a loop of every MDAppt.
        // So for every MDAppt. we check to see if any of the MD Tasks have the same doctor.
        // Notice that this is the first time in the program that we have matched the doctor's name to the database serial number
        // This method also serves as a way to figure out the doctor this applies to so we can put it in the ouput text
        // Kind of like killing two birds with one stone, because we need to make sure the task and the appt. are for the same doctor to determine if their is a conflict, and if their is a conflict we need to know the doctor
        // if the appointment and the task are for the same doctor, AND the dates conflict, we then loop through all the MDappts. to see if their is a date conflict for the day before the Task and any other appts. that doctor has
        // If this turns out to be the case, we want to alert the user to this serious conflict.
        public static List<Conflict> ConflictBuilder(MDAppointment A, List<MDCarePath> MDTasks2, List<MDAppointment> MDAppt)
        {
            List<Conflict> outcon = new List<Conflict>();
            string TaskT = null;
            string contype = null;
            bool Realproblem = false;
            DateTime DayBefore = new DateTime();

            foreach (MDCarePath T in MDTasks2)
            {
                if (A.ResourceSer == 1266 & T.ResourceSer == 1266)  // Dr. Wong
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        continue;
                    }
                    else
                    {
                        DayBefore = T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0));
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 1266 & T.ResourceSer == 1266)
                            {
                                if (tA.Start.Date == DayBefore.Date)
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. Wong", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 1272 & T.ResourceSer == 1272)  // Dr. McKee
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 1272 & T.ResourceSer == 1272)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. McKee", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 1273 & T.ResourceSer == 1273)  // Dr. Nixon
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 1273 & T.ResourceSer == 1273)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. Nixon", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 1964 & T.ResourceSer == 1964)  // Dr. Hunter
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 1964 & T.ResourceSer == 1964)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. Hunter", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 2414 & T.ResourceSer == 2414)  // Dr. Lemons
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 2414 & T.ResourceSer == 2414)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. Lemons", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 2558 & T.ResourceSer == 2558)  // Dr. Jiang
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 2558 & T.ResourceSer == 2558)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add( new Conflict { Doc = "Dr. Jiang", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 2103 & T.ResourceSer == 2103)  // Dr. Osa
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 2103 & T.ResourceSer == 2103)
                            {
                                if (tA.Start.Date == (T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0)).Date))
                                {
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add( new Conflict { Doc = "Dr. Osa", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                    }
                    Realproblem = false;
                }
                else if (A.ResourceSer == 2049 & T.ResourceSer == 2049)  // Dr. Hsu
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        DayBefore = T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0));
                        //System.Windows.Forms.MessageBox.Show("Hsu: " + DayBefore);

                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 2049 & T.ResourceSer == 2049)
                            {
                                //System.Windows.Forms.MessageBox.Show("Appt start date: " + tA.Start + " Hsu: " + DayBefore);

                                if (tA.Start.Date == DayBefore.Date)
                                {
                                   // System.Windows.Forms.MessageBox.Show("Realproblem!");
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if(A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add( new Conflict { Doc = "Dr. Hsu", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                        Realproblem = false;
                    }
                } //end Hsu
                else if(A.ResourceSer == 2557 & T.ResourceSer == 2557)   //Dr. Roberts
                {
                    if (T.DueDate.Date != A.Start.Date)
                    {
                        //not on the same day, don't care
                        continue;
                    }
                    else
                    {
                        DayBefore = T.DueDate.Subtract(new TimeSpan(1, 0, 0, 0));
   
                        //so this means the doctor is OFF or off-site on the day they have an important carepath task due
                        //we only want the program to alert us to situations where the doctor is off the day an important task is due, AND they are off the day before as well. Need an another loop for that.
                        foreach (MDAppointment tA in MDAppt)
                        {
                            if (tA.ResourceSer == 2557 & T.ResourceSer == 2557)
                            {
                                if (tA.Start.Date == DayBefore.Date)
                                {
                                    // System.Windows.Forms.MessageBox.Show("Realproblem!");
                                    Realproblem = true;
                                }
                            }
                        }

                        if (Realproblem == true)
                        {
                            if (T.ActSer == 595)
                            {
                                TaskT = "Approved Plan Review";
                            }
                            else if (T.ActSer == 594)
                            {
                                TaskT = "Review Approved Plan";
                            }
                            else if (T.ActSer == 584)
                            {
                                TaskT = "Contours Needed";
                            }

                            if (A.ActCode == "Contract Off")
                            {
                                contype = "OFF";
                            }
                            else if (A.ActCode.Contains("Vacation"))
                            {
                                contype = "on vacation";
                            }
                            else
                            {
                                contype = "working at a non-Lahey site";
                            }

                            outcon.Add(new Conflict { Doc = "Dr. Roberts", Pat = T.PatName, TaskType = TaskT, Time = A.Start, ConflictType = contype });
                        }
                        Realproblem = false;
                    }
                }
            } // end carepath loop

            return outcon;

        }  // end conflict builder

    }

}




