using System;
using System.Threading;

namespace SimpleThreadPool
{
    class Program
    {
        static void Main(string[] args)
        {
            //Console.WriteLine("Hello World!");


            //var threadEntity = new ThreadEntity(3 * 1000);



            //threadEntity.AssignJob(() => Console.WriteLine("Hello World! 123"));

            var stp = new SimpleThreadPool(threadWaitJobMillisecondsTimeout: 10000);

            for (int count = 0; count < 25; count++)
            {
                stp.QueueUserWorkItem(
                    new WaitCallback(ShowMessage),
                    string.Format("STP1[{0}]", count));
                Thread.Sleep(new Random().Next(500));
            }

            Console.WriteLine("wait stop");
            stp.EndPool();

            Thread.Sleep(1000);



            Console.ReadLine();




        }


        static void ShowMessage(object o)
        {
            string Name;
            //取得執行緒的雜湊碼
            Name = "使用的執行緒編號 Thread ID# = " + Thread.CurrentThread.GetHashCode();
            Console.WriteLine
              ("這是第一組被排入佇列的第 {0} 個工作，{1}", o, Name);
            Console.WriteLine("此工作執行緒結束 !! ");

        }

    }
}
