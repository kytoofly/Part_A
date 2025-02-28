namespace BankingSimulation.Concurrency{
public class ConcurrencyTester
    {
        private List<Customer> customers;
        private List<Teller> tellers;
        private List<BankAccount> accounts;
        private List<Thread> threads;
       
        public ConcurrencyTester(List<Customer> customers, List<Teller> tellers, List<BankAccount> accounts, List<Thread> threads)
        {
            this.customers = customers;
            this.tellers = tellers;
            this.accounts = accounts;
            this.threads = threads;
        }
       
        public void RunTests()
        {
            Console.WriteLine("\n========= CONCURRENCY TESTING RESULTS =========\n");
           
            // Test 1: Verify that threads operated concurrently
            TestThreadConcurrency();
           
            // Test 2: Verify that tellers worked concurrently
            TestTellerConcurrency();
           
            // Test 3: Verify account balance consistency
            TestAccountConsistency();
           
            // Test 4: Verify load balancing among tellers
            TestTellerLoadBalancing();
           
            Console.WriteLine("\n========= END OF TEST =========\n");
        }
       
        private void TestThreadConcurrency()
        {
            Console.WriteLine("Test 1: Thread Concurrency");
           
            
            List<TransactionRecord> allTransactions = new List<TransactionRecord>();
            foreach (var customer in customers)
            {
                allTransactions.AddRange(customer.GetTransactionHistory());
            }
           
            // Sort by start time
            allTransactions = allTransactions.OrderBy(t => t.StartTime).ToList();
           
            // Check for overlapping transactions (proof of concurrency)
            bool concurrencyDetected = false;
           
            for (int i = 0; i < allTransactions.Count - 1; i++)
            {
                if (allTransactions[i].EndTime > allTransactions[i + 1].StartTime)
                {
                    concurrencyDetected = true;
                    Console.WriteLine($"  Concurrency detected: Transaction by Customer {allTransactions[i].CustomerId} overlapped with Customer {allTransactions[i + 1].CustomerId}");
                    Console.WriteLine($"    First transaction: {allTransactions[i].StartTime.ToString("HH:mm:ss.fff")} to {allTransactions[i].EndTime.ToString("HH:mm:ss.fff")}");
                    Console.WriteLine($"    Second transaction: {allTransactions[i + 1].StartTime.ToString("HH:mm:ss.fff")} to {allTransactions[i + 1].EndTime.ToString("HH:mm:ss.fff")}");
                    break;
                }
            }
           
            if (concurrencyDetected)
            {
                Console.WriteLine("  PASS: Threads operated concurrently");
            }
            else
            {
                Console.WriteLine("  FAIL: No thread concurrency detected.");
            }
        }
       
        private void TestTellerConcurrency()
        {
            Console.WriteLine("\nTest 2: Teller Concurrency");
           
            // Getting teller  service times
            bool tellerConcurrencyDetected = false;
           
            for (int i = 0; i < tellers.Count; i++)
            {
                for (int j = i + 1; j < tellers.Count; j++)
                {
                    List<DateTime> startTimesA = tellers[i].GetServiceStartTimes();
                    List<DateTime> endTimesA = tellers[i].GetServiceEndTimes();
                    List<DateTime> startTimesB = tellers[j].GetServiceStartTimes();
                    List<DateTime> endTimesB = tellers[j].GetServiceEndTimes();
                   
                    // Check for overlapping service times
                    for (int a = 0; a < startTimesA.Count; a++)
                    {
                        for (int b = 0; b < startTimesB.Count; b++)
                        {
                            if ((startTimesA[a] <= endTimesB[b] && endTimesA[a] >= startTimesB[b]) ||
                                (startTimesB[b] <= endTimesA[a] && endTimesB[b] >= startTimesA[a]))
                            {
                                tellerConcurrencyDetected = true;
                                Console.WriteLine($"  Teller concurrency detected: Teller {tellers[i].Id} and Teller {tellers[j].Id} worked simultaneously");
                                goto BreakOutOfNestedLoop;
                            }
                        }
                    }
                }
            }
           
            BreakOutOfNestedLoop:
           
            if (tellerConcurrencyDetected)
            {
                Console.WriteLine("  PASS: Tellers operated concurrently");
            }
            else
            {
                Console.WriteLine("  FAIL: No teller concurrency detected.");
            }
        }
       
        private void TestAccountConsistency()
        {
            Console.WriteLine("\nTest 3: Account Balance Consistency");
           
            // Calculate the expected final balance based on initial balances and transactions
            Dictionary<int, decimal> expectedBalances = new Dictionary<int, decimal>();
           
            // Initialize with starting balances
            foreach (var account in accounts)
            {
                expectedBalances[account.Id] = account.GetBalance();
            }
           
            // Applying all transactions to calculate expected balances
            foreach (var customer in customers)
            {
                foreach (var transaction in customer.GetTransactionHistory())
                {
                    // Skipping the failed transactions
                    if (!transaction.Success) continue;
                   
                    
                    expectedBalances[transaction.SourceAccountId] -= transaction.Amount;
                    expectedBalances[transaction.DestinationAccountId] += transaction.Amount;
                }
            }
           
            // Compare expected vs actual balances
            bool allBalancesMatch = true;
           
            foreach (var account in accounts)
            {
                decimal actualBalance = account.GetBalance();
                decimal expectedBalance = expectedBalances[account.Id];
               
                if (Math.Abs(actualBalance - expectedBalance) > 0.001m)  
                {
                    allBalancesMatch = false;
                    Console.WriteLine($"  Balance mismatch in Account {account.Id}: Expected ${expectedBalance}, Actual ${actualBalance}");
                }
            }
           
            if (allBalancesMatch)
            {
                Console.WriteLine("  PASS: All account balances match their expected values");
            }
            else
            {
                Console.WriteLine("  FAIL: Some account balances don't match their expected values");
            }
        }
       
        private void TestTellerLoadBalancing()
        {
            Console.WriteLine("\nTest 4: Teller Load Balancing");
           
            // Get number of customers served by each teller
            Dictionary<int, int> tellerLoads = new Dictionary<int, int>();
           
            foreach (var teller in tellers)
            {
                tellerLoads[teller.Id] = teller.GetCustomersServed();
                Console.WriteLine($"  Teller {teller.Id} served {tellerLoads[teller.Id]} customers");
            }
           
            
            double averageLoad = tellerLoads.Values.Average();
            double maxDeviation = tellerLoads.Values.Max() - tellerLoads.Values.Min();
            double percentDeviation = (maxDeviation / averageLoad) * 100;
           
            Console.WriteLine($"  Average customers per teller: {averageLoad:F2}");
            Console.WriteLine($"  Maximum deviation: {maxDeviation:F2} customers ({percentDeviation:F2}%)");
           
            // Evaluate load balancing - allowing up to 50% deviation for random assignment
            if (percentDeviation <= 50)
            {
                Console.WriteLine("  PASS: Teller workload is reasonably balanced");
            }
            else
            {
                Console.WriteLine("  WARNING: Teller workload is significantly imbalanced");
            }
        }
    }
}