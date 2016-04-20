/**
 * 网络包结构:
 * packet head 8字节-[(消息ID)uint + (消息大小)uint]
 * packet body 包体N字节-[根据包头中后4个字节即包体大小]
 * 
 * packet = head + body
 * 
 * 用户可以根据自己情况扩展自己的包格式，并修改对应格式
 * 
 */

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;

namespace net_plugin.net
{
    /// <summary>
    /// Desc: 客户端Tcp
    /// Author: xiangjinbao
    /// </summary>
    public class TcpNet : NetObject
    {
        /// <summary>
        /// Tcp Socket对象
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
        /// 客户端发送缓冲区
        /// </summary>
        public byte[] mSendBuffer = null;
        /// <summary>
        /// 客户端接受缓冲区
        /// </summary>
        public byte[] mRevcBuffer = null;
        /// <summary>
        /// Tcp锁
        /// </summary>
        private readonly object mTcpLock = new object();
        /// <summary>
        /// 消息开关
        /// </summary>
        private bool mTcpSwitch = true;
        /// <summary>
        /// 接收消息大小
        /// </summary>
        private int mRevcSize = 0;
        /// <summary>
        /// 消息接受标示位置
        /// </summary>
        private int mRevcPos = 0;

        /// <summary>
        /// 链接IP
        /// </summary>
        private string mIp = string.Empty;
        /// <summary>
        /// 链接端口
        /// </summary>
        private int mPort = -1;
        
        public TcpNet()
        {
            mSendBuffer = new byte[MAX_BUFFER_SIZE];
            mRevcBuffer = new byte[MAX_BUFFER_SIZE];

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
        /// 链接Socket
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        public override void Connect(string ip, int port)
        {
            lock (mTcpLock)
            {
                this.mIp = ip;
                this.mPort = port;
                if (null != mSocket && mSocket.Connected)
                {
                    throw new Exception("Tcp Socket Connect Exception");
                }
                try
                {
                    mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    mSocket.ReceiveBufferSize = MAX_BUFFER_SIZE;
                    mSocket.NoDelay = true;
                    mSocket.Connect(mIp, mPort);
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
                catch(Exception e)
                {
                    throw new Exception("Tcp Socket Connect Error " + e.ToString());
                }
            }
        }

        /// <summary>
        /// 断开链接
        /// </summary>
        public override void DisConnect()
        {
            if (mSocket != null)
            {
                mSocket.Close();
            }
            lock (mTcpLock)
            {
                if (mSocket.Connected)
                {
                    mSocket.Disconnect(true);
                    mSocket = null;
                }
            }
            mSendThread = null;
            mRevcThread = null;
            GC.Collect();
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        private void AsyncSend()
        {
            while (mTcpSwitch)
            {
                DoSend();
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 处理发送包
        /// </summary>
        private void DoSend()
        {
            lock (mTcpLock)
            {
                if ((null == mSocket) || !mSocket.Connected)
                {
                    return;
                }
            }
            int totalLength = 0;
            // 合并发送消息包
            lock (mSendLock)
            {
                if (mSendQueue.Count == 0)
                {
                    return;
                }
                while ((totalLength < MAX_BUFFER_SIZE) && mSendQueue.Count > 0)
                {
                    byte[] packet = mSendQueue.Peek();
                    if (totalLength + packet.Length < MAX_BUFFER_SIZE)
                    {
                        packet.CopyTo(mSendBuffer, totalLength);
                        totalLength += packet.Length;
                        mSendQueue.Dequeue();
                    }
                    else
                    {
                        break;
                    }
                }
            }
            // 发送消息
            try
            {
                mSocket.Send(mSendBuffer, 0, totalLength, SocketFlags.None);
                // 清空发送缓存
                System.Array.Clear(mSendBuffer, 0, MAX_BUFFER_SIZE);
            }
            catch (Exception e)
            {
                throw new Exception("Send Buffer Error " + e.ToString());
            }
        }

        /// <summary>
        /// 异步接受消息
        /// </summary>
        private void AsyncRevc()
        {
            do
            {
                mRevcSize = 0;
                try
                {
                    int size = MAX_BUFFER_SIZE - mRevcPos;
                    if (size > 0)
                    {
                        mRevcSize = mSocket.Receive(mRevcBuffer, mRevcPos, size, SocketFlags.None);
                        mRevcPos += mRevcSize;
                        if (mRevcSize == 0)
                        {
                            mRevcSize = 1;
                        }
                    }
                    else
                    {
                        mRevcSize = 1;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception("Tcp Revice Byte Error " + e.ToString());
                }
                DoRevc();
            } while (mRevcSize > 0);
        }

        /// <summary>
        /// 处理接受到消息
        /// </summary>
        private void DoRevc()
        { 
            try
            {
                int offset = 0;
                while (mRevcPos > PACKET_HEAD_SIZE)
                {
                    uint packetBodySize = BitConverter.ToUInt32(mRevcBuffer, 5);
                    int packetSize = (int)(PACKET_HEAD_SIZE + packetBodySize);
                    if (mRevcBuffer.Length >= packetSize)
                    {
                        byte[] packet = new byte[packetSize];
                        Buffer.BlockCopy(mRevcBuffer, offset, packet, 0, packetSize);
                        lock (mRevcLock)
                        {
                            mRevcQueue.Enqueue(packet);
                        }
                        mRevcPos -= packetSize;
                        offset += packetSize;
                    }
                    else
                    {
                        break;
                    }
                }
                // 整理RecvBuffer, 将buffer 内容前移
                Buffer.BlockCopy(mRevcBuffer, offset, mRevcBuffer, 0, mRevcPos);
            }
            catch (Exception e)
            {
                throw new Exception("DoRevc Buffer Error " + e.ToString());
            }
        }

        /// <summary>
        /// 是否链接
        /// </summary>
        /// <returns></returns>
        public override bool Connected()
        {
            if (null != mSocket && mSocket.Connected)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 网络Ip
        /// </summary>
        public string Ip
        {
            get 
            {
                return mIp;
            }
        }

        /// <summary>
        /// 网络Port
        /// </summary>
        public int Port
        {
            get
            {
                return mPort;
            }
        }
    }
}
