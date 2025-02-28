using System.Diagnostics;
namespace BankingSimulation.stresstest{
    public class BankingStressTest
{
    private List<BankAccount> accounts;
    private List<Thread> customerThreads;
    private List<Customer> customers;
    private List<Teller> tellers;
    private int operationsCompleted = 0;
    private int operationsFailed = 0;
    private readonly object counterLock = new object();
    private Stopwatch stopwatch = new Stopwatch();
    private bool isRunning = false;
    private DateTime lastProgressUpdate = DateTime.MinValue;

    // Stress test configuration
    private int customerCount;
    private int operationsPerCustomer;
    private int accountCount;
    private bool useProgressReporting;

    public BankingStressTest(int customerCount = 50, int operationsPerCustomer = 20,
                          int accountCount = 5, bool useProgressReporting = true)
    {
        this.customerCount = customerCount;
        this.operationsPerCustomer = operationsPerCustomer;
        this.accountCount = accountCount;
        this.useProgressReporting = useProgressReporting;
    }

    public void RunStressTest()
    {
        Console.WriteLine("\n========== BANKING SYSTEM STRESS TEST ==========\n");
        Console.WriteLine($"Configuration:");
        Console.WriteLine($"- Customer Threads: {customerCount}");
        Console.WriteLine($"- Operations Per Customer: {operationsPerCustomer}");
        Console.WriteLine($"- Total Accounts: {accountCount}");
        Console.WriteLine($"- Total Operations: {customerCount * operationsPerCustomer}");
        Console.WriteLine();

        // Initialize the banking system
        SetupBankingSystem();

        // Start the stress test
        isRunning = true;
        stopwatch.Start();

        if (useProgressReporting)
        {
            // Start progress reporting in a separate thread
            Thread progressThread = new Thread(ReportProgress);
            progressThread.IsBackground = true;
            progressThread.Start();
        }

        // Start all customer threads
        Console.WriteLine("Starting all customer threads...");
        foreach (var thread in customerThreads)
        {
            thread.Start();
        }

        // Wait for all threads to complete
        foreach (var thread in customerThreads)
        {
            thread.Join();
        }

        // Stop the test
        stopwatch.Stop();
        isRunning = false;

        
        ReportFinalResults();
    }

    private void SetupBankingSystem()
    {
        // Creating bank accounts with varying initial balances
        accounts = new List<BankAccount>();
        for (int i = 1; i <= accountCount; i++)
        {
            accounts.Add(new BankAccount(i, 1000 * i)); // Accounts with $1000, $2000, etc.
        }

        
        tellers = new List<Teller>();
        int tellerCount = Math.Max(5, customerCount / 10); // 1 teller per 10 customers, minimum 5
        for (int i = 1; i <= tellerCount; i++)
        {
            tellers.Add(new Teller(i));
        }

        
        customers = new List<Customer>();
        customerThreads = new List<Thread>();

        for (int i = 1; i <= customerCount; i++)
        {
            string name = $"Customer-{i}";
            Customer customer = new StressTestCustomer(name, accounts, tellers,
                                                   operationsPerCustomer, this);
            customers.Add(customer);
           
            Thread thread = new Thread(customer.PerformTransfersWithTellers);
            customerThreads.Add(thread);
        }
    }

    private void ReportProgress()
    {
        while (isRunning)
        {
            // Update progress every second
            if ((DateTime.Now - lastProgressUpdate).TotalSeconds >= 1)
            {
                lastProgressUpdate = DateTime.Now;
               
                int currentOps;
                int failedOps;
               
                lock (counterLock)
                {
                    currentOps = operationsCompleted;
                    failedOps = operationsFailed;
                }
               
                double elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
                int totalOperations = customerCount * operationsPerCustomer;
                double percentComplete = (double)currentOps / totalOperations * 100;
                double throughput = currentOps / elapsedSeconds;
               
                Console.WriteLine($"[Progress] {currentOps}/{totalOperations} operations completed ({percentComplete:F2}%) | " +
                                 $"Throughput: {throughput:F2} ops/sec | Failed: {failedOps} | Elapsed: {elapsedSeconds:F2}s");
               
                // Check for potential system stability issues
                if (throughput < 0.5 && currentOps > 10)
                {
                    Console.WriteLine("WARNING: System throughput is very low. Possible stability issue detected.");
                }
            }
           
            Thread.Sleep(200); // Sleeping to prevent loop
        }
    }

    private void ReportFinalResults()
    {
        double elapsedSeconds = stopwatch.ElapsedMilliseconds / 1000.0;
        int totalOperations = operationsCompleted;
        int totalFailed = operationsFailed;
        double throughput = totalOperations / elapsedSeconds;
       
        Console.WriteLine("\n========== STRESS TEST RESULTS ==========\n");
        Console.WriteLine($"Test Duration: {elapsedSeconds:F2} seconds");
        Console.WriteLine($"Total Operations Completed: {totalOperations}");
        Console.WriteLine($"Failed Operations: {totalFailed}");
        Console.WriteLine($"Operation Throughput: {throughput:F2} operations/second");
       
        // Report account balances
        Console.WriteLine("\nFinal Account Balances:");
        decimal totalBalance = 0;
        foreach (var account in accounts)
        {
            decimal balance = account.GetBalance();
            totalBalance += balance;
            Console.WriteLine($"Account {account.Id}: ${balance:F2}");
        }
        Console.WriteLine($"Total Balance Across All Accounts: ${totalBalance:F2}");
       
        // Report teller workload distribution
        Console.WriteLine("\nTeller Workload Distribution:");
        int totalCustomersServed = tellers.Sum(t => t.GetCustomersServed());
        foreach (var teller in tellers)
        {
            int customersServed = teller.GetCustomersServed();
            double percentage = (double)customersServed / totalCustomersServed * 100;
            Console.WriteLine($"Teller {teller.Id}: {customersServed} customers ({percentage:F2}%)");
        }
       
        // Calculating the contention metrics
        double averageOperationsPerSecond = throughput;
        double theoreticalMaxThroughput = customerCount / 0.1; 
        double contentionFactor = 1 - (averageOperationsPerSecond / theoreticalMaxThroughput);
       
        Console.WriteLine("\nPerformance Analysis:");
        Console.WriteLine($"Theoretical Max Throughput: {theoreticalMaxThroughput:F2} ops/sec");
        Console.WriteLine($"Actual Throughput: {averageOperationsPerSecond:F2} ops/sec");
        Console.WriteLine($"Resource Contention Factor: {contentionFactor:F2} (0 = No contention, 1 = Complete blocking)");
       
        // Overall assessment
        Console.WriteLine("\nOverall System Stability Assessment:");
        if (operationsFailed == 0 && contentionFactor < 0.7)
        {
            Console.WriteLine("PASSED: System remained stable under high load with acceptable throughput");
        }
        else if (operationsFailed == 0 && contentionFactor < 0.9)
        {
            Console.WriteLine("MARGINALLY PASSED: System remained stable but showed high contention");
        }
        else if (operationsFailed > 0 && operationsFailed < totalOperations * 0.01)
        {
            Console.WriteLine("WARNING: System mostly stable but had minor failures");
        }
        else
        {
            Console.WriteLine("FAILED: System showed significant instability under stress");
        }
       
        Console.WriteLine("\n========== END OF STRESS TEST ==========\n");
    }

    // Method for customer threads to report completed operations
    public void ReportOperationCompleted(bool success)
    {
        lock (counterLock)
        {
            if (success)
                operationsCompleted++;
            else
                operationsFailed++;
        }
    }

    // Internal class for stress testing
    private class StressTestCustomer : Customer
    {
        private int operationsToPerform;
        private BankingStressTest stressTest;
        private Random random = new Random();

        public StressTestCustomer(string name, List<BankAccount> accounts, List<Teller> tellers,
                             int operationsToPerform, BankingStressTest stressTest)
            : base(name, accounts, tellers)
        {
            this.operationsToPerform = operationsToPerform;
            this.stressTest = stressTest;
        }

        public override void PerformTransfersWithTellers()
        {
            try
            {
                Console.WriteLine($"Customer {Id} ({Name}) started stress testing operations");
               
                for (int i = 0; i < operationsToPerform; i++)
                {
                    // Choose random accounts for transfer
                    BankAccount sourceAccount = accounts[random.Next(accounts.Count)];
                    BankAccount destAccount;
                    do
                    {
                        destAccount = accounts[random.Next(accounts.Count)];
                    } while (destAccount.Id == sourceAccount.Id);
                   
                    // Random amount between $5 and $50
                    decimal amount = random.Next(5, 51);
                   
                    // Select a random teller with exponential backoff on contention
                    Teller teller = null;
                    bool tellerFound = false;
                    int retryCount = 0;
                   
                    // Simulate peak load situations where tellers may be busy
                    while (!tellerFound && retryCount < 3)
                    {
                        teller = tellers[random.Next(tellers.Count)];
                       
                        // randomly failing to get a teller sometimes to simulate congestion
                        tellerFound = (random.NextDouble() > 0.1 * retryCount);
                       
                        if (!tellerFound)
                        {
                            retryCount++;
                            Thread.Sleep(random.Next(50, 200) * retryCount); 
                        }
                    }
                   
                    if (tellerFound)
                    {
                        try
                        {
                            // Create transaction record
                            TransactionRecord transaction = new TransactionRecord
                            {
                                CustomerId = Id,
                                CustomerName = Name,
                                TellerId = teller.Id,
                                SourceAccountId = sourceAccount.Id,
                                DestinationAccountId = destAccount.Id,
                                Amount = amount,
                                StartTime = DateTime.Now
                            };
                           
                            // Perform the transfer
                            teller.ServeCustomer(this, sourceAccount, destAccount, amount);
                           
                            // Complete the transaction record
                            transaction.EndTime = DateTime.Now;
                            transaction.Success = true;
                           
                            // Record successful operation
                            RecordTransaction(transaction);
                            stressTest.ReportOperationCompleted(true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in customer {Id} operation: {ex.Message}");
                            stressTest.ReportOperationCompleted(false);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Customer {Id} couldn't find an available teller after {retryCount} attempts");
                        stressTest.ReportOperationCompleted(false);
                    }
                   
                    // Minimal delay between operations to maximize stress
                    Thread.Sleep(random.Next(10, 50));
                }
               
                Console.WriteLine($"Customer {Id} ({Name}) completed all stress testing operations");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error in customer {Id} thread: {ex.Message}");
            }
        }
    }
}
}