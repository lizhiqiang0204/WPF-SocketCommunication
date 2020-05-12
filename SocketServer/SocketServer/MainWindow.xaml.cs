using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SocketServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<Socket> clientScoketLis = new List<Socket>();//存储连接服务器端的客户端的Socket
        public MainWindow()
        {
            InitializeComponent();
            //Loaded += MainWindow_Loaded;
            btnStartServer.Click += BtnStartServer_Click;//事件注册
            btnSendMsg.Click += BtnSendMsg_Click;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            //使用foreach出现 “集合已修改；可能无法执行枚举操作”,ClientExit源于方法中对list集合进行了Remove，所造成的异常。
            //msdn的解释：foreach 语句是对枚举数的包装，它只允许从集合中读取，不允许写入集合。也就是,不能在foreach里遍历的时侯把它的元素进行删除或增加的操作的
            //foreach (var socket in clientScoketLis)
            //{
            //    ClientExit(null , socket);
            //}
            //改成for循环即可
            for (int i = 0; i < clientScoketLis.Count; i++)//向每个客户端说我下线了
            {
                ClientExit(null, clientScoketLis[i]);
            }
        }

         private void ClientExit(string msg, Socket proxSocket)
        {
            AppendTxtLogText(msg);
            clientScoketLis.Remove(proxSocket);//移除集合中的连接Socket
             try
             {
                if (proxSocket.Connected)//如果是连接状态
                {
                     proxSocket.Shutdown(SocketShutdown.Both);//关闭连接
                     proxSocket.Close(100);//100秒超时间
                 }
             }
             catch (Exception ex)
             {
             }
        }

         /// <summary>
        /// 向文本框中追加信息
       /// </summary>
        /// <param name="str"></param>
         private void AppendTxtLogText(string str)
         {
            if (!(txtLog.Dispatcher.CheckAccess()))//判断跨线程访问
             {
                 ////同步方法
                 //this.Dispatcher.Invoke(new Action<string>( s => 
                //{
                //    this.txtLog.Text = string.Format("{0}\r\n{1}" , s , txtLog.Text);
                 //}) ,str);
                //异步方法
                 this.Dispatcher.BeginInvoke(new Action<string>(s =>
                 {
                     this.txtLog.Text = string.Format("{0}\r\n{1}", s, txtLog.Text);
                 }), str);
             }
            else
             { 
             this.txtLog.Text = string.Format("{0}\r\n{1}", str, txtLog.Text);
             }
         }

        private void BtnSendMsg_Click(object sender, RoutedEventArgs e)
        {
            foreach (Socket proxSocket in clientScoketLis)
            {
                if (proxSocket.Connected)//判断客户端是否还在连接
                {
                    byte[] data = Encoding.Default.GetBytes(this.txtMsg.Text);
                    //6、发送消息
                    proxSocket.Send(data, 0, data.Length, SocketFlags.None); //指定套接字的发送行为
                    //this.txtMsg.Text = null;
                }
            }
        }

        private void BtnStartServer_Click(object sender, RoutedEventArgs e)
        {
            //1、创建Socket对象
            //参数：寻址方式，当前为Ivp4  指定套接字类型   指定传输协议Tcp；
            Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //2、绑定端口、IP
            IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Parse(this.txtIp.Text), int.Parse(txtPort.Text));
            socket.Bind(iPEndPoint);
            //3、开启侦听   10为队列最多接收的数量
            socket.Listen(10);//如果同时来了100个连接请求，只能处理一个,队列中10个在等待连接的客户端，其他的则返回错误消息。
            
            //4、开始接受客户端的连接  ，连接会阻塞主线程，故使用线程池。
            ThreadPool.QueueUserWorkItem(new WaitCallback(AcceptClientConnect), socket);
        }

          private void AcceptClientConnect(object obj)
          {
              //转换Socket
              var serverSocket = obj as Socket;
  
              AppendTxtLogText("服务端开始接收客户端连接！");
  
              //不断接受客户端的连接
              while (true)
              {
                  //5、创建一个负责通信的Socket
                  Socket proxSocket = serverSocket.Accept();
                  AppendTxtLogText(string.Format("客户端：{0}连接上了！", proxSocket.RemoteEndPoint.ToString()));
                  //将连接的Socket存入集合
                  clientScoketLis.Add(proxSocket);
                  //6、不断接收客户端发送来的消息
                  ThreadPool.QueueUserWorkItem(new WaitCallback(ReceiveClientMsg) , proxSocket);
             }
        }

            /// <summary>
         /// 不断接收客户端信息子线程方法
         /// </summary>
         /// <param name="obj">参数Socke对象</param>
         private void ReceiveClientMsg(object obj)
         {
             var proxSocket = obj as Socket;
             //创建缓存内存，存储接收的信息   ,不能放到while中，这块内存可以循环利用
             byte[] data = new byte[1020 * 1024];
             while (true)
             {
                int len;
                try
                 {
                     //接收消息,返回字节长度
                     len = proxSocket.Receive(data, 0, data.Length, SocketFlags.None);
                 }
                catch (Exception ex)
                 {
                     //7、关闭Socket
                     //异常退出
                    try
                     {
                         ClientExit(string.Format("客户端：{0}非正常退出", proxSocket.RemoteEndPoint.ToString()), proxSocket);
                    }
                     catch (Exception)
                     {
                     }
                     return;//让方法结束，终结当前客户端数据的异步线程，方法退出，即线程结束
                 }
 
                 if (len <= 0)//判断接收的字节数
                 {
                     //7、关闭Socket
                     //小于0表示正常退出
                    try
                     {
                         ClientExit(string.Format("客户端：{0}正常退出", proxSocket.RemoteEndPoint.ToString()), proxSocket);
                     }
                     catch (Exception)
                     {
                     }
                     return;//让方法结束，终结当前客户端数据的异步线程，方法退出，即线程结束
                 }
                 //将消息显示到TxtLog
                 string msgStr = Encoding.Default.GetString(data, 0, len);
                 //拼接字符串
                 AppendTxtLogText(string.Format("接收到客户端：{0}的消息：{1}" , proxSocket.RemoteEndPoint.ToString() , msgStr));
             }
         }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            //ClientWindows clientWindows = new ClientWindows();
            //clientWindows.Show();
        }
    }
}
