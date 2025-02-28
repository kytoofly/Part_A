// See https://aka.ms/new-console-template for more information
using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using BankingSimulation.Validation;
using BankingSimulation.Concurrency;
using BankingSimulation.stresstest;

namespace BankingSimulation
{
    public class BankAccount
{
    public int Id { get; }
    public decimal Balance { get; private set; }
    // Mutex for the account to protect it
    public readonly object LockObject = new object();

    public BankAccount(int id, decimal initialBalance)
    {
        Id = id;
        Balance = initialBalance;
    }

    
    public void Deposit(decimal amount)
    {
        Thread.Sleep(100); // Simulating the processing time
        Balance += amount;
        Console.WriteLine($"Deposited ${amount} to Account {Id}. New balance: ${Balance}");
    }

    
    public bool Withdraw(decimal amount)
    {
        Thread.Sleep(100); 
        
        if (Balance >= amount)
        {
            Balance -= amount;
            Console.WriteLine($"Withdrew ${amount} from Account {Id}. New balance: ${Balance}");
            return true;
        }
        
        Console.WriteLine($"Failed to withdraw ${amount} from Account {Id}. Insufficient funds. Current balance: ${Balance}");
        return false;
    }

    // Method to transfer money to another account 
    public bool TransferTo(BankAccount destinationAccount, decimal amount, int maxRetries = 3)
    {
        // Resource Ordering - deadlock technique
        BankAccount firstLock = this.Id < destinationAccount.Id ? this : destinationAccount;
        BankAccount secondLock = this.Id < destinationAccount.Id ? destinationAccount : this;
        
        bool isSourceFirst = (this.Id == firstLock.Id);
        
        int retryCount = 0;
        bool transferComplete = false;

        while (!transferComplete && retryCount <= maxRetries)
        {
            // Timeout Mechanism - deadlock technique
            bool firstLockAcquired = false;
            bool secondLockAcquired = false;
            
            try
            {
                // Trying t acquire first lock with timeout
                Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Attempting to acquire lock on Account {firstLock.Id}");
                firstLockAcquired = Monitor.TryEnter(firstLock.LockObject, 1000); 
                
                if (firstLockAcquired)
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Acquired lock on Account {firstLock.Id}");
                    
                    // Simulate some processing time
                    Thread.Sleep(100);
                    
                    // Trying to acquire second lock with timeout
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Attempting to acquire lock on Account {secondLock.Id}");
                    secondLockAcquired = Monitor.TryEnter(secondLock.LockObject, 1000); 
                    
                    if (secondLockAcquired)
                    {
                        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Acquired lock on Account {secondLock.Id}");
                        
                        // if both locks are acquired perform the transfer
                        if (isSourceFirst)
                        {
                            // this account is source
                            if (Balance >= amount)
                            {
                                // Withdraw from this account
                                Balance -= amount;
                                // Deposit to destination account
                                destinationAccount.Balance += amount;
                                
                                Console.WriteLine($"Transferred ${amount} from Account {Id} to Account {destinationAccount.Id}");
                                Console.WriteLine($"Account {Id} balance: ${Balance}");
                                Console.WriteLine($"Account {destinationAccount.Id} balance: ${destinationAccount.Balance}");
                                
                                transferComplete = true;
                            }
                            else
                            {
                                Console.WriteLine($"Failed to transfer ${amount} from Account {Id} to Account {destinationAccount.Id}. Insufficient funds.");
                                transferComplete = true; 
                            }
                        }
                        else
                        {
                            // Reverse order: destination account is first, this account is second
                            if (Balance >= amount)
                            {
                                
                                Balance -= amount;
                                
                                destinationAccount.Balance += amount;
                                
                                Console.WriteLine($"Transferred ${amount} from Account {Id} to Account {destinationAccount.Id}");
                                Console.WriteLine($"Account {Id} balance: ${Balance}");
                                Console.WriteLine($"Account {destinationAccount.Id} balance: ${destinationAccount.Balance}");
                                
                                transferComplete = true;
                            }
                            else
                            {
                                Console.WriteLine($"Failed to transfer ${amount} from Account {Id} to Account {destinationAccount.Id}. Insufficient funds.");
                                transferComplete = true; 
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Timed out waiting for lock on Account {secondLock.Id}");
                        // Second lock couldn't be acquired - will retry
                        retryCount++;
                        Console.WriteLine($"Transfer attempt failed. Retrying... (Attempt {retryCount} of {maxRetries})");
                        
                        // Random backoff before retrying
                        Random random = new Random();
                        int backoffTime = random.Next(100, 500) * retryCount;  // Exponential backoff
                        Console.WriteLine($"Backing off for {backoffTime}ms before retry");
                        Thread.Sleep(backoffTime);
                    }
                }
                else
                {
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Timed out waiting for lock on Account {firstLock.Id}");
                    // First lock couldn't be acquired - will retry
                    retryCount++;
                    Console.WriteLine($"Transfer attempt failed. Retrying... (Attempt {retryCount} of {maxRetries})");
                    
                    // Random backoff before retrying
                    Random random = new Random();
                    int backoffTime = random.Next(100, 500) * retryCount;  // Exponential backoff
                    Console.WriteLine($"Backing off for {backoffTime}ms before retry");
                    Thread.Sleep(backoffTime);
                }
            }
            finally
            {
                // Always release locks in reverse order of acquisition
                if (secondLockAcquired)
                {
                    Monitor.Exit(secondLock.LockObject);
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Released lock on Account {secondLock.Id}");
                }
                
                if (firstLockAcquired)
                {
                    Monitor.Exit(firstLock.LockObject);
                    Console.WriteLine($"Thread {Thread.CurrentThread.ManagedThreadId}: Released lock on Account {firstLock.Id}");
                }
            }
        }
        
        if (!transferComplete)
        {
            Console.WriteLine($"Transfer failed after {maxRetries} attempts. Operation aborted.");
        }
        
        return transferComplete;
    }

    // Get balance with mutex protection
    public decimal GetBalance()
    {
        lock (LockObject)
        {
            return Balance;
        }
    }
}
   
    // Add a Teller class to simulate bank employees
    public class Teller
    {
        public int Id { get; }
        private int customersServed = 0;
        private readonly object lockObject = new object();
       
        // Track when this teller starts and finishes serving customers
        private List<DateTime> serviceStartTimes = new List<DateTime>();
        private List<DateTime> serviceEndTimes = new List<DateTime>();
       
        public Teller(int id)
        {
            Id = id;
        }
       
        public void ServeCustomer(Customer customer, BankAccount sourceAccount, BankAccount destAccount, decimal amount)
        {
            lock (lockObject)
            {
                customersServed++;
            }
           
            DateTime startTime = DateTime.Now;
            serviceStartTimes.Add(startTime);
           
            Console.WriteLine($"[{startTime.ToString("HH:mm:ss.fff")}] Teller {Id} is serving Customer {customer.Id} ({customer.Name}) for transfer ${amount} from Account {sourceAccount.Id} to Account {destAccount.Id}");
           
            // Process the transfer
            sourceAccount.TransferTo(destAccount, amount);
           
            DateTime endTime = DateTime.Now;
            serviceEndTimes.Add(endTime);
           
            Console.WriteLine($"[{endTime.ToString("HH:mm:ss.fff")}] Teller {Id} completed serving Customer {customer.Id} ({customer.Name})");
        }
       
        public int GetCustomersServed()
        {
            lock (lockObject)
            {
                return customersServed;
            }
        }
       
        public List<DateTime> GetServiceStartTimes()
        {
            return serviceStartTimes;
        }
       
        public List<DateTime> GetServiceEndTimes()
        {
            return serviceEndTimes;
        }
    }
   
    // Customer class to work with tellers
    public class Customer
{
    private static int nextId = 1;
    public int Id { get; }
    public string Name { get; }
    protected List<BankAccount> accounts;
    protected List<Teller> tellers;
    private Random random = new Random();
   
    // Track transactions for validation
    private readonly List<TransactionRecord> transactionHistory = new List<TransactionRecord>();
    private readonly object historyLock = new object();
   
    public Customer(string name, List<BankAccount> accounts, List<Teller> tellers)
    {
        Id = nextId++;
        Name = name;
        this.accounts = accounts;
        this.tellers = tellers;
    }
   
    // Make this virtual to allow overriding
    public virtual void PerformTransfersWithTellers()
    {
         Console.WriteLine($"Customer {Id} ({Name}) started banking operations");

            // Perform 3 random transfers
               for (int i = 0; i < 3; i++)
              {
                // Randomly select source and destination accounts
                int sourceIndex = random.Next(accounts.Count);
                 int destIndex;
                do
                {   
                    destIndex = random.Next(accounts.Count);
                } 
                while (destIndex == sourceIndex); // Ensure source and destination are different

                BankAccount sourceAccount = accounts[sourceIndex];
                BankAccount destAccount = accounts[destIndex];
 

                decimal amount = random.Next(10, 101);

                Teller teller = tellers[random.Next(tellers.Count)];

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

                // Request teller to perform the transfer
                 teller.ServeCustomer(this, sourceAccount, destAccount, amount);

                // Complete the transaction record
                transaction.EndTime = DateTime.Now;
                transaction.Success = true;

                // Record the transaction
                RecordTransaction(transaction);

                // Sleep for a random duration between transfers
                 Thread.Sleep(random.Next(100, 301));
            }
               Console.WriteLine($"Customer {Id} ({Name}) completed all banking operations");
 }
    
   
    // Record a transaction
    public void RecordTransaction(TransactionRecord transaction)
    {
        lock (historyLock)
        {
            transactionHistory.Add(transaction);
        }
    }
   
    // Get transaction history
    public List<TransactionRecord> GetTransactionHistory()
    {
        lock (historyLock)
        {
            return transactionHistory.ToList();
        }
    }
}

    // Record class to track transactions for testing
    public class TransactionRecord
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public int TellerId { get; set; }
        public int SourceAccountId { get; set; }
        public int DestinationAccountId { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Success { get; set; }
    }
   
  
   
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Banking Simulation - Synchronization and then Concurrency Test");
            Console.WriteLine("----------------------------------------");
           
            // Create bank accounts
            List<BankAccount> accounts = new List<BankAccount>
            {
                new BankAccount(1, 1000),
                new BankAccount(2, 2000),
                new BankAccount(3, 3000)
            };
           
            // Create tellers
            List<Teller> tellers = new List<Teller>
            {
                new Teller(1),
                new Teller(2),
                new Teller(3)
            };
           
            // Create customers
            List<Customer> customers = new List<Customer>
            {
                new Customer("Alissa", accounts, tellers),
                new Customer("Dave", accounts, tellers),
                new Customer("Charles", accounts, tellers),
                new Customer("Dia", accounts, tellers),
                new Customer("Evan", accounts, tellers),
                new Customer("Kylan", accounts, tellers),
                new Customer("Gio", accounts, tellers),
                new Customer("Hans", accounts, tellers)
            };
           
            // Create threads for each customer
            List<Thread> threads = new List<Thread>();
           
            foreach (var customer in customers)
            {
                Thread customerThread = new Thread(customer.PerformTransfersWithTellers);
                threads.Add(customerThread);
                Console.WriteLine($"Created thread for Customer {customer.Id} ({customer.Name})");
            }
           
            Console.WriteLine("\nStarting all customer threads...\n");
           
            // Start stopwatch for performance measurement
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
           
            // Start all threads
            foreach (var thread in threads)
            {
                thread.Start();
            }
           
            // Wait for all threads to complete
            foreach (var thread in threads)
            {
                thread.Join();
            }
           
            // Stop the stopwatch
            stopwatch.Stop();
           
            Console.WriteLine($"\nAll customer transactions completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine("\nFinal account balances:");
           
            foreach (var account in accounts)
            {
                Console.WriteLine($"Account {account.Id}: ${account.GetBalance()}");
            }
           
            // Running the concurrency tests
            ConcurrencyTester tester = new ConcurrencyTester(customers, tellers, accounts, threads);
            tester.RunTests();
            // Running the synchronization tests
            SynchronizationValidator.PerformSynchronizationTests(accounts);
            // Run stress testing
           Console.WriteLine("\nPreparing for stress testing...");
           Console.WriteLine("Press any key to begin stress test with 50 concurrent customers...");
           Console.ReadKey(true);

          // Create and run the stress test
          BankingStressTest stressTest = new BankingStressTest(
          customerCount: 50,          // 50 concurrent customers
          operationsPerCustomer: 20,  // perform 20 operations
          accountCount: 5,            // 5 bank 
          useProgressReporting: true  // Showing real-time progress // 
          );

           stressTest.RunStressTest();

           
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    } 
}


