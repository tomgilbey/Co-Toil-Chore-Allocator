//Written by Tom Gilbey.

using System.Diagnostics;

namespace Co_Toil_Project
{
    public class ChoresAllocator
    {
        private static ChoresAllocator? instance; //Declare instance of ChoresAllocator, possibly null
        private static readonly object MUTEX = new object(); //Declare MUTEX object to lock instance
        private AssignedChoresList assignedChores; //Declare assignedChores object of type AssignedChoresList, this holds the list of assigned chores

        /*  This method is used to get the instance of the ChoresAllocator class. If the instance is null, a new instance is created.
         *  The method is locked to prevent multiple threads from creating multiple instances of the class.
         *  Ensures that a single instance of the object is used across the application.
         *  Returns: instance of ChoresAllocator class
        */
        public static ChoresAllocator Instance //Declare public static Instance property of type ChoresAllocator
        {
            get //Getter method for the property
            {
                lock (MUTEX) //Ensures only one thread can access the critical section at a time, which is the code below.
                {
                    if (instance == null) //This checks if the instance exists
                    {
                        instance = new ChoresAllocator(); //if the instance doesn't exist, a new instance is created
                    }

                    return instance; //Return the instance of the class
                }
            }
        }
        /* Constructor for the ChoresAllocator class, it sets up the ChoresAllocator object
         * It initialises the assignedChores object of type AssignedChoresList
         */ 
        private ChoresAllocator()
        {
            assignedChores = AssignedChoresList.Instance;
        }

        /* This method is used to calculate the total chore time for each user. 
         * It loops through each chore and adds the time to the total for each user.
         * It returns an array of the total time for each user.
         */

        private int[] getTotals(ChoreDisplayDetails[] chores) //Method to calculate the total chore time for each user, takes an array of ChoreDisplayDetails as a parameter
        {
            int[] totals = new int[2]; //Declare an array of integers to hold the total time for each user

            foreach (ChoreDisplayDetails details in chores) //Loop through each chore in the array
            {
                for (int i = 0; i < details.Estimate.Length; i++) //Loop through each user estimate for the chore
                {
                    totals[i] += details.Estimate[i]; //Add the estimate to the total for the user
                }
            }

            return totals; //Return the array of totals
        }

        /* This method is used to update the loads in the database for each user. 
         * It takes the loads for each user as parameters and updates the loads in the database depending on whos is bigger. 
         * This is called at the end of the algorithm.
         */

        private void updateLoads(double User1Load, double User2Load) 
        {
            if (User1Load < User2Load) //If the load for user 1 is less than the load for user 2
            {
                List<double> loadToAdd = new List<double> { 0.0, User2Load - User1Load }; //Create a list of doubles to hold the loads to add. Because user 1 has less load, user 2's load is subtracted from user 1's load to get the difference.
                DatabaseHandler.Instance.setLoads(loadToAdd); //Set the loads in the database
            }
            else if (User2Load < User1Load)
            {
                List<double> loadToAdd = new List<double> { User1Load - User2Load, 0.0 };
                DatabaseHandler.Instance.setLoads(loadToAdd);
            }
            else
            {
                List<double> loadToAdd = new List<double> { 0.0, 0.0 }; //If the loads are equal, no loads need to be added
                DatabaseHandler.Instance.setLoads(loadToAdd); //Set the loads in the database
            }
        }

        /* This method is used to process the chores for each user. 
         * It takes the normalised time for each user, the load for the user, the normalised time for the other user and the user number as parameters.
         * It processes the chores for each user and adds them to the assigned chores list.
         */

        private void processUserChores(List<Tuple<string, double>> userNormalisedTime, ref double userLoad, ref List<Tuple<string, double>> otherUserNormalisedTime, int userNumber)
        {
            int i = 0; //Declare an integer to ensure that 
            for (int j = 0; j != userNormalisedTime.Count + 1; j++) //Loop through the normalised time for the user
            {
                if (!(i == userNormalisedTime.Count || i == otherUserNormalisedTime.Count)) //Checks if i is within the bounds of the normalised time for the user and the other user.
                {
                    double userHighest = userNormalisedTime[i].Item2; //Get the highest normalised time for the user
                    double? comparison = otherUserNormalisedTime.FirstOrDefault(t => t.Item1 == userNormalisedTime[i].Item1)?.Item2; //Get the normalised time for the other user
                    if ((i < userNormalisedTime.Count || i < otherUserNormalisedTime.Count) && userHighest < comparison) //If the user's highest normalised time is less than the other user's normalised time
                    {
                        string name = userNormalisedTime[i].Item1.Split("_")[0]; //Split the chore name and day from the normalised time, store the chore name
                        string day = userNormalisedTime[i].Item1.Split("_")[1]; //Split the chore name and day from the normalised time, store the day
                        Chore c = new Chore(name, day, userNormalisedTime[i].Item2, false, userNumber); //Create a new chore object with the chore name, day, normalised time, false for not an exception and the user number
                        assignedChores.addChore(c); //Add the new chore to the assigned chores list
                        userLoad += userNormalisedTime[i].Item2; //Add the normalised time to the user's load
                        otherUserNormalisedTime.RemoveAll(t => t.Item1 == $"{name}_{day}"); //Remove the chore from the other user's normalised time
                        userNormalisedTime.RemoveAt(i); //Remove the chore from the current user's normalised time
                        break; //Break out of the loop
                    }
                    else //If the user's highest normalised time is greater than the other user's normalised time, increment i
                    {
                        i++;
                    }
                }
                else //If i isnt within the bound, assign the first chore to the user
                {
                    string name = userNormalisedTime[0].Item1.Split("_")[0];
                    string day = userNormalisedTime[0].Item1.Split("_")[1];
                    Chore c = new Chore(name, day, userNormalisedTime[0].Item2, false, userNumber);
                    assignedChores.addChore(c);
                    userLoad += userNormalisedTime[0].Item2;
                    otherUserNormalisedTime.RemoveAll(t => t.Item1 == $"{name}_{day}");
                    userNormalisedTime.RemoveAt(0);
                    break;
                }
            }
        }

        /* This method is used to allocate chores to each user. 
         * It gets the loads for each user from the db and normalises the time for each chore for each user.
         * It then allocates the chores to each user based on the normalised time.
         * It then updates the loads in the database.
         */

        public void allocateChores(ChoreDisplayDetails[] chores)
        {
            if (chores.Length == 0) { return; } //If there are no chores, return

            double[] loads = DatabaseHandler.Instance.getLoads().ToArray(); //Get the loads for each user from the database
            int[] total = getTotals(chores); //Get the total time for each user

            List<Tuple<string, double>> UserOneNormalisedTime = new List<Tuple<string, double>>(); //Declare a list of tuples to hold the normalised times for user 1
            List<Tuple<string, double>> UserTwoNormalisedTime = new List<Tuple<string, double>>(); //Declare a list of tuples to hold the normalised times for user 2

            for (int i = 0; i < chores.Length; i++) //Loop through the chores
            {
                ChoreDisplayDetails chore = chores[i]; //Get the chore
                UserOneNormalisedTime.Add(Tuple.Create($"{chore.Name}_{chore.Day}", Math.Round((double)(chore.Estimate[0] / (double)total[0]), 3))); //Add the chore name and normalised time to the list for user 1
                UserTwoNormalisedTime.Add(Tuple.Create($"{chore.Name}_{chore.Day}", Math.Round((double)(chore.Estimate[1] / (double)total[1]), 3))); //Add the chore name and normalised time to the list for user 2
            }

            foreach (ChoreDisplayDetails chore in chores) //Loop through each chore
            {
                if (chore.Exception == 1) //If the chore has an exception for user 1
                {
                    string assignedChoreName = $"{chore.Name}_{chore.Day}"; //Get the chore name
                    UserOneNormalisedTime.RemoveAll(t => t.Item1 == assignedChoreName); //Remove the chore from the list for user 1
                    UserOneNormalisedTime.Add(Tuple.Create($"{chore.Name}_{chore.Day}", 1.5)); //Add the chore to the list for user 1 with a normalised time of 1.5, so the user shouldn't be assigned this chore

                }
                else if (chore.Exception == 2) //If the chore has an exception for user 2
                {
                    string assignedChoreName = $"{chore.Name}_{chore.Day}";
                    UserTwoNormalisedTime.RemoveAll(t => t.Item1 == assignedChoreName);
                    UserTwoNormalisedTime.Add(Tuple.Create($"{chore.Name}_{chore.Day}", 1.5));
                }
                else //If the chore has no exception 
                {
                    break; //Break out of the loop
                }
            }

            UserOneNormalisedTime.Sort((x, y) => y.Item2.CompareTo(x.Item2)); //Sort the list of normalised times for user 1 into descending order.
            UserTwoNormalisedTime.Sort((x, y) => y.Item2.CompareTo(x.Item2)); //Sort the list of normalised times for user 2 into descending order.

            double User1Load = loads[0]; //Get the load for user 1
            double User2Load = loads[1]; //Get the load for user 2

            while (UserOneNormalisedTime.Count() > 0 && UserTwoNormalisedTime.Count() > 0) //While both users have chores to be assigned
            {
                if (User1Load <= User2Load) //If the load for user 1 is less than or equal to the load for user 2
                {
                    processUserChores(UserOneNormalisedTime, ref User1Load, ref UserTwoNormalisedTime, 1); //Process the chores for user 1, using ProcessUserChores method. Pass in the normalised time for user 1, a reference to the normalised time for user 2 and the user number
                }
                else if (User1Load > User2Load) //If the load for user 1 is greater than the load for user 2
                {
                    processUserChores(UserTwoNormalisedTime, ref User2Load, ref UserOneNormalisedTime, 2); //Process the chores for user 2, using ProcessUserChores method. Pass in the normalised time for user 2, a reference to the normalised time for user 1 and the user number
                }
            }

            updateLoads(User1Load, User2Load); //Call the updateLoads method to update the loads in the database.

        }
    }
}