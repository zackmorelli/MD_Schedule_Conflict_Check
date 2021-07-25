using System;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;


/*
    
    MD_CarePath_Schedule_Conflict_Check 
    This program requires .NET Framework 4.6.1
    Copyright (C) 2021 Zackary Thomas Ricci Morelli
    
    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <https://www.gnu.org/licenses/>.

    I can be contacted at: zackmorelli@gmail.com


    Release 2.0 7/12/2021


*/



namespace MD_CarePath_Schedule_Conflict_Check
{
    // the top-level "Executive" class of this Executable Program! 
    class MD_CarePath_Schedule_Conflict_Check
    {

        // Executable Main Method
        static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            //Starts GUI on a separate Task 
            Task.Run(() => System.Windows.Forms.Application.Run(new SimpleGUI()));

            string ConnectionString = @"Data Source=WVARIASDBP01SS;Initial Catalog=VARIAN;Integrated Security=true;";
            //@"Data Source=WVARIASDBP01SS;Initial Catalog=VARIAN;Integrated Security=true;";

            SqlConnection conn = new SqlConnection(ConnectionString);
            SqlCommand command;
            SqlDataReader datareader;
            string sql;

            //The SQL commands here are just strings, so I've made the variables below for the puposes of inserting the datetimes we are interested in as strings, in the proper format, into the SQL commands
            //You'll notice that in the SQL string below, that when the strings representing the datetimes are added to the SQL string using string concatenation, there are single quotes around the double quotes used to indicate
            //the start and end of a string. These single quotes are actaully part of the SQL syntax for datetimes, and they are absolutley neccessary.
            DateTime RN = DateTime.Now;
            DateTime twentyfivedaysago = RN.Subtract(new TimeSpan(25, 0, 0, 0));
            DateTime CareCut = RN.AddDays(25.0);
            string SQLRN = RN.ToString("MM/dd/yyyy");
            string SQL25pdays = CareCut.ToString("MM/dd/yyyy");
            string SQL25mdays = twentyfivedaysago.ToString("MM/dd/yyyy");

            //These are the list we'll use to store the doctor appointments and carepath tasks. MDTasks2 and MDAppt2 are the final lists with just the stuff we care about that we'll use at the end of the program.
            //You'll notice I made custom classes for the MD tasks and appointments. These are defined in the HelperCalsses file. They store all the information we'll need about the tasks and appointments.
            List<MDCarePath> MDTasks = new List<MDCarePath>(); // CarePath tasks that are only assigned to MDs (like Contours needed or plan review)
            List<MDCarePath> MDTasks2 = new List<MDCarePath>();
            List<MDAppointment> MDAppt = new List<MDAppointment>(); // All recent appointments (Scheduable activities) that all the MDs have. This is whittled down to the specific appts we are looking for.           
            List<MDAppointment> MDAppt2 = new List<MDAppointment>();

           // MessageBox.Show("Trig 1");

            //So we are going to start by finding the MD appointments
            //Unfortunatley, the way appointments work behind the scenes in Aria is vert segmented, and actaully object-oriented I would say.
            //ScheduledActivity is the huge table of ALL the appointments in Aria. Unfortunatley, this table does not tell you what the appointment actaully is. It gives you serial numbers for entries in other tables that have that information.
            //so we start by simply querying for all the appointments that are scheduled for the next 25 days. We can't be more specific than that.
            //For each appt. we add a new MDAppt. object to the MDAppt list.
            conn.Open();
            sql = "USE VARIAN SELECT ScheduledActivitySer, ActivityInstanceSer, ActivityNote, ScheduledStartTime, ScheduledEndTime FROM dbo.ScheduledActivity WHERE ScheduledStartTime BETWEEN '" + SQLRN + "' AND '" + SQL25pdays + "'"; // This queries the special ResourceActivity table to get all Scheduled Activities that every MD Resource has for the next 15 days.
            command = new SqlCommand(sql, conn);
            datareader = command.ExecuteReader();

            while (datareader.Read())
            {
                MDAppt.Add(new MDAppointment { SchedActSer = (long)datareader["ScheduledActivitySer"], ActIntSer = (long)datareader["ActivityInstanceSer"], ActNote = (datareader["ActivityNote"] as string) ?? null, Start = Convert.ToDateTime(datareader["ScheduledStartTime"]), Stop = Convert.ToDateTime(datareader["ScheduledEndTime"]) });
            }
            conn.Close();
            // MessageBox.Show(MDAppt.Count.ToString());

            //For each appt. in the next 25 days, we then do 2 queries each to get all the information we need.
            //This is of course very inefficient, but the organization of the Varian database forces us to do this
            foreach (MDAppointment A in MDAppt)
            {
                conn.Open();
                sql = "USE VARIAN SELECT ActivitySer FROM dbo.ActivityInstance WHERE ActivityInstanceSer  = " + A.ActIntSer; // Queries ActivityInstance to get the ActivitySer associated with the ActivityInstance for each MD appt. This is an annoying in-between step that we need to do so we can query Activity for the activity code, that way we can finally figure out what the hell the appt. actually is. Because most of them we don't need.
                command = new SqlCommand(sql, conn);
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    A.ActSer = (long)datareader["ActivitySer"];
                }
                conn.Close();

                //MessageBox.Show("Trig3");
                conn.Open();
                sql = "USE VARIAN SELECT ActivityCode FROM dbo.Activity WHERE ActivitySer  = " + A.ActSer; // Gets the ActivityCode (Which is really the appt. type, like "consult", "TX IMRT", "simulation", etc.) for each MD appt.
                command = new SqlCommand(sql, conn);
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    A.ActCode = (datareader["ActivityCode"] as string) ?? null;
                }
                conn.Close();

                //count++;
                //Dialog.Text = count + " / " + MDAppt.Count;
                //CountBox(count + " / " + MDAppt.Count);
            }

            //MessageBox.Show("Trig 2");
            //So we now have a list of all the appts. in the next 25 days with all of the relevant info we need for each appt.
            // we can finally whittle down these appts. to what is actually relevant for this - one line with LINQ!
            MDAppt.RemoveAll(Ap => (Ap.ActCode != "Contract Off" & Ap.ActCode != "Administration" & !Ap.ActCode.Contains("Vacation")));

            //using (StreamWriter Lwrite = File.AppendText(@"C:\prog\TEST1.txt"))
            //{
            //    foreach (MDAppointment tp in MDAppt)
            //    {
            //        Lwrite.WriteLine(tp.ToString());
            //    }
            //}

            // This deals with shenanigans with the Appointments and the Activity notes in particular. Everything that is not Contract Off, Administration, and Vacation has been removed, but the notes for the administration appointments still have to be dealt with. This actually puts everything we care about in a new list to avoid complications with deleting things from the list.
            //We are specifically only concerned with "Administration" appt. with the note "Not at this site". This is just how the secretaries make the appointments, I don't why it is done this way.
            foreach (MDAppointment neji in MDAppt)
            {
                //MessageBox.Show("APT INIT.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);

                if (neji.ActCode == null | neji.ActCode == "")
                {
                    //MessageBox.Show("APT NULL.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);
                    continue;
                }

                if (neji.ActCode == "Administration")
                {
                    if (neji.ActNote == null | neji.ActNote == "")
                    {
                        continue;
                    }

                    //MessageBox.Show("APT ADMIN.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);
                    if ((neji.ActNote.Contains("Not") | neji.ActNote.Contains("not")) & (neji.ActNote.Contains("Site") | neji.ActNote.Contains("site") | neji.ActNote.Contains("sitte") | neji.ActNote.Contains("Sitte")))
                    {
                        // MessageBox.Show("APT ADMIN GOOD.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);
                        MDAppt2.Add(neji);
                    }
                }
                else if (neji.ActCode == "Contract Off")
                {
                    //MessageBox.Show("APT OFF.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);
                    MDAppt2.Add(neji);
                }
                else if (neji.ActCode.Contains("Vacation"))
                {
                    //MessageBox.Show("APT.   Actcode: " + neji.ActCode + " ActNote: " + neji.ActNote);
                    MDAppt2.Add(neji);
                }
            }

            // MessageBox.Show("MD Appointments: " + MDAppt.Count.ToString());

            //MessageBox.Show("Trig 3");

            //Another annoying feature of appointments in the back-end of Aria is that the resources (like people or machines) attached to a given appointment, which one can easily see in the interface, are nowhere to be found in any of the appt. related tables
            //instead, what we have to do is query this super-special "ResourceActivity" view table which is like a major lynchpin of the database for some reason. This is one of the tables that Entity Framework refuses to use, which makes it unusable.
            //So the list we have in MDAppt2 now is only for MD appointments, because those appts. are only made for the MDs. But, to figure out which doctor the appts. are for, we need to query ResourceActivity
            foreach (MDAppointment apt in MDAppt2)
            {
                conn.Open();
                sql = "USE VARIAN SELECT ResourceSer FROM dbo.ResourceActivity WHERE ScheduledActivitySer = " + apt.SchedActSer;
                command = new SqlCommand(sql, conn);
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    apt.ResourceSer = (long)datareader["ResourceSer"];
                }
                conn.Close();

                //count++;
                //Dialog.Text = count + " / " + MDAppt2.Count;
            }

            //using (StreamWriter Lwrite = File.AppendText(@"C:\prog\TEST4.txt"))
            //{
            //    foreach (MDAppointment tp in MDAppt2)
            //    {
            //        Lwrite.WriteLine(tp.ToString());
            //    }
            //}


            //Now we can get the MD CarePath Tasks
            //==============================================================================================================================================================================================================================================================
     
            //We start by getting all the Carepath tasks that have been edited in the past 25 days or future 25 days (Which should be impossible i guess, but for thoroughness)
            conn.Open();
            sql = "USE VARIAN SELECT ActivityInstanceSer, ActivitySer FROM dbo.vv_ActivityInstance WHERE HstryDateTime BETWEEN '" + SQL25mdays + "' AND '" + SQL25pdays + "'";  //recently edited Tasks (like a care path task) that are only assigned to MDs (like Contours Needed and Plan Review). Task Type determined by ActSer.
            command = new SqlCommand(sql, conn);
            datareader = command.ExecuteReader();

            while (datareader.Read())
            {
                //makes a new MDTask object for each carepath task and adds it to the MDTask list
                MDTasks.Add(new MDCarePath { ActIntSer = (long)datareader["ActivityInstanceSer"], ActSer = (long)datareader["ActivitySer"] });

                //count++;
                //Dialog.Text = count.ToString();
            }
            conn.Close();
            // MessageBox.Show(MDTasks.Count.ToString());

            foreach (MDCarePath path in MDTasks)
            {
                //These are the activity serial numbers for the Contours Needed and Plan Review Tasks, which I figured out by looking through the table in SSMS 
                //We make a new list of only these types of carepath tasks
                if (path.ActSer == 595 | path.ActSer == 594 | path.ActSer == 584)
                {
                    MDTasks2.Add(new MDCarePath { ActIntSer = path.ActIntSer, ActSer = path.ActSer });
                }
            }
            // MessageBox.Show(MDTasks2.Count.ToString());


           // MessageBox.Show("Trig 4");
            //count = 0;
            foreach (MDCarePath T in MDTasks2)
            {
                //so now we find when these MD care path tasks are due and what patient they are for.
                conn.Open();
                sql = "USE VARIAN SELECT DueDateTime, PatientSer, NonScheduledActivityCode FROM dbo.NonScheduledActivity WHERE ActivityInstanceSer = " + T.ActIntSer;  
                command = new SqlCommand(sql, conn);
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    T.DueDate = (DateTime)datareader["DueDateTime"];
                    T.PatSer = (long)datareader["PatientSer"];
                    T.Status = (datareader["NonScheduledActivityCode"] as string) ?? null;
                }
                conn.Close();


                // Use the PatientDoctor table to look up the Doctor of the patient associated with this Care Path Task. Problem is this table includes referring physicians, which is whittled out using the oncologist flag
                // However, there are still a ton of old oncologists in the table, so I include logic in case the ResourceSer does not specifically match to the numbers of the current doctors.
                // Also need the primary flag in case the patient has come back for retreatment and is being treated by a different doctor than the first time. Hopefully this covers everything but I'm not 100% sure.
                conn.Open();
                sql = "USE VARIAN SELECT ResourceSer FROM dbo.PatientDoctor WHERE OncologistFlag = 1 AND PrimaryFlag = 1 AND PatientSer = " + T.PatSer;  
                command = new SqlCommand(sql, conn);                                                                                    
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    T.ResourceSer = (long)datareader["ResourceSer"];
                }
                conn.Close();

                // Get the patient Name.
                conn.Open();
                sql = "USE VARIAN SELECT FirstName, LastName FROM dbo.Patient WHERE PatientSer = " + T.PatSer;  
                command = new SqlCommand(sql, conn);
                datareader = command.ExecuteReader();

                while (datareader.Read())
                {
                    T.PatName = ((datareader["FirstName"] as string) ?? null) + " " + ((datareader["LastName"] as string) ?? null);
                }
                conn.Close();

                //count++;
                //Dialog.Text = count + " / " + MDTasks2.Count;
            }


            MDTasks2.RemoveAll(ta => (ta.PatSer.Equals(0)));

            // also going to remove any "completed" tasks so the conflict analysis runs only on non-completed tasks 
            MDTasks2.RemoveAll(tas => (tas.Status.Equals("Completed")));

            // MessageBox.Show(MDTasks2.Count.ToString());

            //using (StreamWriter Lwrite = File.AppendText(@"C:\prog\TEST2.txt"))
            //{
            //    foreach (MDCarePath sk in MDTasks2)
            //    {
            //        Lwrite.WriteLine(sk.ToString());
            //    }
            //}

            //This is a safety check
            foreach (MDCarePath Ta in MDTasks2)
            {
                if (Ta.ResourceSer != 1266 & Ta.ResourceSer != 1272 & Ta.ResourceSer != 1273 & Ta.ResourceSer != 1964 & Ta.ResourceSer != 2414 & Ta.ResourceSer != 2558 & Ta.ResourceSer != 2103 & Ta.ResourceSer != 2049 & Ta.ResourceSer != 2557)
                {
                    MessageBox.Show("There is an MD CarePath Task within the next 15 days for a Radiation Oncologist other than one of the 8 working for Lahey as of 7/13/2021. This means that this program may not return correct results. \n\n This probably happened because a patient who was treated by a doctor who no longer works at Lahey in the past is currently being retreated and their primary oncologist has not been updated in ARIA, or a new doctor started working for us. Let Zack Morelli know if a new doctor needs to be added to the program. \n\n Note: This is neccessary because our database is not well-maintained and contains many old doctors who no longer work for Lahey.");
                    MessageBox.Show(Ta.ResourceSer.ToString());
                }
            }

            //MessageBox.Show("Trig 5");

            //MessageBox.Show("Trig 2");

            // loop through the appts. for each MD and then find if they have tasks during that time
            //=============================================================================================================================================================================================================

            List<Conflict> intermedcon;
            List<Conflict> Conflicts = new List<Conflict>();

            //The Conflict builder method below does a lot of work. You'll have to look in the HelperClasses file to see it.
            foreach (MDAppointment A in MDAppt2)
            {
                intermedcon = Conflict.ConflictBuilder(A, MDTasks2, MDAppt2);
                foreach(Conflict c in intermedcon )
                {
                    if(c.Doc != null)
                    {
                        Conflicts.Add(c);
                    }
                }
            }

            //using (StreamWriter Lwrite = File.AppendText(@"C:\prog\TEST3.txt"))
            //{
            //    foreach (Conflict conair in Conflicts)
            //    {
            //        Lwrite.WriteLine(conair.ToString());
            //    }
            //}

            string intermed = null;
            string FinalMessage = null;

            for (int i = 0; i < Conflicts.Count; i++)
            {
                if (i == Conflicts.Count - 1)
                {
                    break;
                }
                else
                {
                    for (int j = i + 1; j < Conflicts.Count; j++)
                    {
                        if (Conflicts[j].Equals(Conflicts[i]))
                        {
                            Conflicts.RemoveAt(j);
                        }
                    }
                }
            }


            //This is the output of the program, which is made very easy by using the Conflict class, where I have overidden the ToString method to be in the format I want here.
            if (Conflicts.Count == 0)
            {
                FinalMessage = "There are no conflicts with the MDs schedules and their Care Path tasks for the next 25 days - ending on " + CareCut;
            }
            else
            {
                FinalMessage = "There are " + Conflicts.Count + " conflicts between the MDs schedules and their Care Path tasks for the next 25 days - ending on " + CareCut + ".\n\n\n" + "This program only displays conflicts where the Doctor is unavailable both on the day an important task (not yet completed) assigned to them is due AND the day before. \n\n\n";
                Conflicts.Sort();

                foreach (Conflict conflict in Conflicts)
                {
                    intermed = conflict.ToString();
                    FinalMessage = FinalMessage + intermed + "\n\n";
                }
            }

            MessageBox.Show(FinalMessage);

        } // end of Main
    } // end of "Executive" class
} // end of namespace



    