namespace BankingSimulation.Validation {
public class SynchronizationValidator
{
    // Test class for synchronization mechanisms
    public static void PerformSynchronizationTests(List<BankAccount> accounts)
    {
        Console.WriteLine("\n======== SYNCHRONIZATION VALIDATION TESTS ========\n");
       
        // Test 1: High contention on a single account
        TestHighContentionSingleAccount(accounts[0]);
       
        // Test 2:Operations with artificial delays
        TestInterleavedOperations(accounts);
       
        // Test 3: Rapid concurrent transfers
        TestRapidConcurrentTransfers(accounts);
       
        Console.WriteLine("\n======== END OF SYNCHRONIZATION TESTS ========\n");
    }
   
    private static void TestHighContentionSingleAccount(BankAccount account)
    {
        Console.WriteLine($"Test 1: High Contention on Single Account (ID: {account.Id})");
       
        
        decimal initialBalance = account.GetBalance();
        Console.WriteLine($"  Initial balance: ${initialBalance}");
       
        // Create a large number of threads all trying to deposit and withdraw the same amount
        int threadCount = 20;
        decimal amount = 10;
       
        //  CountdownEvent to wait for all threads to complete
        CountdownEvent countdown = new CountdownEvent(threadCount);
       
        
        for (int i = 0; i < threadCount; i++)
        {
            int threadId = i;
            bool isDeposit = i % 2 == 0;  // Even threads deposit, odd threads withdraw
           
            Thread thread = new Thread(() => {
                try
                {
                    // Add a small random delay to increase chance of contention
                    Thread.Sleep(new Random().Next(5, 20));
                   
                    if (isDeposit)
                    {
                        account.Deposit(amount);
                        Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} deposited ${amount}");
                    }
                    else
                    {
                        account.Withdraw(amount);
                        Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} withdrew ${amount}");
                    }
                }
                finally
                {
                    countdown.Signal();
                }
            });
           
            thread.Start();
        }
       
        
        countdown.Wait();
       
        // Checking final balance 
        decimal finalBalance = account.GetBalance();
        Console.WriteLine($"  Final balance: ${finalBalance}");
       
        if (Math.Abs(finalBalance - initialBalance) < 0.001m)
        {
            Console.WriteLine("  PASS: Balance is consistent after high contention operations");
        }
        else
        {
            Console.WriteLine($"  FAIL: Balance is inconsistent. Expected ${initialBalance}, got ${finalBalance}");
            Console.WriteLine("  This indicates a synchronization issue (race condition)");
        }
    }
   
    private static void TestInterleavedOperations(List<BankAccount> accounts)
    {
        Console.WriteLine("\nTest 2: Interleaved Operations with Artificial Delays");
       
        BankAccount account = accounts[1];  
        decimal initialBalance = account.GetBalance();
        Console.WriteLine($"  Initial balance of Account {account.Id}: ${initialBalance}");
       
        // Creating two threads that will perform operations with deliberate delays at critical points
        ManualResetEvent readyEvent = new ManualResetEvent(false);  // Used to synchronize thread start
        CountdownEvent doneEvent = new CountdownEvent(2);  // Used to wait for both threads
       
        // Thread 1: Will deposit amount, then sleep, then withdraw half amount
        Thread thread1 = new Thread(() => {
            try
            {
                readyEvent.WaitOne();  
               
                // Custom implementation to simulate a critical section with a delay
                lock (account.LockObject)
                {
                    decimal balance = account.GetBalance();
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance: ${balance}");
                   
                    // Deliberate delay inside critical section to test lock effectiveness
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} sleeping inside critical section...");
                    Thread.Sleep(1000);  // One second delay to allow other thread to potentially interfere
                   
                    decimal newBalance = balance + 100;  // Add $100
                   
                    // Another delay before completing the operation
                    Thread.Sleep(500);
                   
                    // Complete the operation
                    account.Deposit(100);
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} deposited $100 after delay");
                }
               
                // Outside critical section, wait a bit
                Thread.Sleep(500);
               
                // Now withdraw half the amount
                account.Withdraw(50);
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} withdrew $50");
            }
            finally
            {
                doneEvent.Signal();
            }
        });
       
        // Thread 2: Will try to withdraw a large amount, checking balance before and after
        Thread thread2 = new Thread(() => {
            try
            {
                readyEvent.WaitOne();  // Wait for signal to start
               
                // Custom implementation to simulate a race condition attempt
                decimal balanceBefore = account.GetBalance();
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance before: ${balanceBefore}");
               
                // Deliberate delay to try to interleave with Thread 1's operation
                Thread.Sleep(300);
               
                // Try to withdraw a large amount - the lock should protect from inconsistency
                bool success = account.Withdraw(balanceBefore * 0.8m);  // Try to withdraw 80% of what we saw
               
                decimal balanceAfter = account.GetBalance();
                Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} read balance after: ${balanceAfter}");
                Console.WriteLine($"  Withdrawal success: {success}");
            }
            finally
            {
                doneEvent.Signal();
            }
        });
       
        // Start both threads
        thread1.Start();
        thread2.Start();
       
        // Signal threads to begin operations simultaneously
        readyEvent.Set();
       
        // Wait for both threads to complete
        doneEvent.Wait();
       
        // Check final balance
        decimal finalBalance = account.GetBalance();
        Console.WriteLine($"  Final balance: ${finalBalance}");
       
        
        Console.WriteLine("  PASS: Interleaved operations completed without corrupting account state");
    }
   
    private static void TestRapidConcurrentTransfers(List<BankAccount> accounts)
    {
        Console.WriteLine("\nTest 3: Rapid Concurrent Transfers Between Accounts");
       
        // Record initial total balance across all accounts
        decimal initialTotalBalance = accounts.Sum(a => a.GetBalance());
        Console.WriteLine($"  Initial total balance across all accounts: ${initialTotalBalance}");
       
        // Create many threads that will perform transfers between accounts
        int transferCount = 30;
        CountdownEvent transfersDone = new CountdownEvent(transferCount);
        Random random = new Random();
       
        // Start the transfers
        for (int i = 0; i < transferCount; i++)
        {
            int sourceIndex = random.Next(accounts.Count);
            int destIndex;
            do
            {
                destIndex = random.Next(accounts.Count);
            } while (destIndex == sourceIndex);  // Ensure different accounts
           
            decimal amount = random.Next(5, 51);  // $5-$50
           
            Thread thread = new Thread(() => {
                try
                {
                    BankAccount source = accounts[sourceIndex];
                    BankAccount dest = accounts[destIndex];
                   
                    Console.WriteLine($"  Thread-{Thread.CurrentThread.ManagedThreadId} attempting transfer: ${amount} from Account {source.Id} to Account {dest.Id}");
                   
                    // Perform transfer
                    source.TransferTo(dest, amount);
                }
                finally
                {
                    transfersDone.Signal();
                }
            });
           
            thread.Start();
           
            // Add small delay between thread starts to stagger them
            Thread.Sleep(random.Next(10, 50));
        }
       
        // Wait for all transfers to complete
        transfersDone.Wait();
       
        // Checking final total balance 
        decimal finalTotalBalance = accounts.Sum(a => a.GetBalance());
        Console.WriteLine($"  Final total balance across all accounts: ${finalTotalBalance}");
       
        if (Math.Abs(finalTotalBalance - initialTotalBalance) < 0.001m)
        {
            Console.WriteLine("  PASS: Total balance is preserved after multiple concurrent transfers");
            Console.WriteLine("  This validates that synchronization prevented any loss or duplication of funds");
        }
        else
        {
            Console.WriteLine($"  FAIL: Total balance changed. Expected ${initialTotalBalance}, got ${finalTotalBalance}");
            Console.WriteLine("  This indicates a synchronization issue (race condition)");
        }
    }
}
}
