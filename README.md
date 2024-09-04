# MD_Schedule_Conflict_Check

This program is for use with ARIA/Eclipse, which is a commerical radiation treatment planning software suite made by Varian Medical Systems which is used in Radiation Oncology. This is one of several scripts which I have made while working in the Radiation Oncology department at Lahey Hospital and Medical Center in Burlington, MA. I have licensed it under GPL V3 so it is open-source and publicly.

There is also a .docx README file in the repo that describes what the program does and how it is organized.

MD_Schedule_Conflict_Check is not an ESAPI (Eclipse Scripting API) script, but a standalone Windows Forms program. It accesses ARIA/Eclispe and makes queries against the ARIA variansystem database in order to analyze the clinic schedule to find conflicts in the doctor's schedules. This is useful because ARIA has no feature for dealing with scheduling conflicts. Unfortunatley, the ARIA scheduling module (carepath) doesn't have any rules preventing scheduling conflicts and it doesn't have any notifications of any kind to alert clinic staff to conflicts. The biggest problem with this is if a doctor misses an appointment that was on the clinic schedule due to a conflict no one noticed and that ARIA is not capable of noticing. So this program was developed in order to find MD schedule conflicts ahead of time. Please note that schedule/carepath info in ARIA is not accessible via ESAPI. All schedule info is retrieved by querying the ARIA variansystem DB directly.
