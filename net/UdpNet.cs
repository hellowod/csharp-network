/**
 * 网络包结构:
 * 这里使用者可以自定义消息包结构
 * 
 * 注意点：UDP目前只支持发单独一个数据报道服务器，不能将多个数据报合并
 * 成一个发送，这里需要优化。
 * 
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace net_plugin.net
{
    public class UdpNet : NetObject
    {
        /// <summary>
        /// 客户端Socket
        /// </summary>
        private Socket mSocket;
        /// <summary>
        /// 发送消息线程
        /// </summary>
        private Thread mSendThread;
        /// <summary>
        /// 接受消息线程
        /// </summary>
        private Thread mRevcThread;
        /// <summary>
        /// 消息接受
        /// </summary>
        private byte[] mRecvBuffer; 
        /// <summary>
        /// Tcp锁
        /// </summary>
        private readonly object mTcpLock = new object();
        /// <summary>
        /// 消息开关
        /// </summary>
        private bool mUdpSwitch = true;
        /// <summary>
        /// 接收消息大小
        /// </summary>
        private int mRevcSize = 0;

        /// <summary>
        /// 客户端Ip
        /// </summary>
        private string mIp = string.Empty;
        /// <summary>
        /// 客户端端口
        /// </summary>
        private int mPort = -1;
        /// <summary>
        /// 发送端地址
        /// </summary>
        private IPEndPoint mEndPoint;

        public UdpNet()
        {
            mRecvBuffer = new byte[MAX_BUFFER_SIZE];

            this.Init();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void Init()
        {
            if (null == mSendThread)
            {
                mSendThread = new Thread(new ThreadStart(AsyncSend));
                mSendThread.IsBackground = true;
            }
            if (!mSendThread.IsAlive)
            {
                mSendThread.Start();
            }
        }

        /// <summary>
        /// 链接服务器
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public override void Connect(string ip, int port)
        {
            mIp = ip;
            mPort = port;
            mEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Udp);
            mSocket.ReceiveBufferSize = MAX_BUFFER_SIZE;
            mSocket.NoDelay = true;

            // 启动接受线程
            if (null == mRevcThread)
            {
                mRevcThread = new Thread(new ThreadStart(AsyncRevc));
                mRevcThread.IsBackground = true;
            }
            if (!mRevcThread.IsAlive)
            {
                mRevcThread.Start();
            }
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        private void AsyncSend()
        {
            while (mUdpSwitch)
            {
                try
                {
                    if (mSendQueue.Count > 0)
                    {
                        lock (mSendLock)
                        {
                            byte[] packet = mSendQueue.Peek();
                            mSocket.SendTo(packet, SocketFlags.None, mEndPoint);
                            mSendQueue.Dequeue();
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Send Packet Error: " + e.ToString());
                }
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 异步接受消息
        /// </summary>
        private void AsyncRevc()
        {
            do
            {
                EndPoint remotePoint = null;
                mRevcSize = 0;
                try
                {
                    mRevcSize = mSocket.ReceiveFrom(mRecvBuffer, SocketFlags.None, ref remotePoint);
                    if (mRevcSize == 0)
                    {
                        mRevcSize = 1;
                    }
                    else
                    {
                        byte[] packet = new byte[mRevcSize];
                        mRecvBuffer.CopyTo(packet, 0);
                        lock (mRevcLock)
                        {
                            mRevcQueue.Enqueue(packet);
                        }
                        Array.Clear(mRecvBuffer, 0, mRevcSize);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Tcp Revice Byte Error " + e.ToString());
                }
            } while (mRevcSize > 0);
        }
        
    }
}
