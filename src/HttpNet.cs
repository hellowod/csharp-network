/**
 * 网络包结构:
 * packet head 4字节-[(消息ID)uint]
 * packet body 包体N字节-[]
 * 
 * httpurl = url + head
 * 
 * packet = head + body
 * 
 * 用户可以根据自己情况扩展自己的包格式，并修改对应格式
 * 
 * 注意：暂时Http不支持合并包发送的情况，故如果想同步消息，得采用其他方式，
 * 后面的优化版本将会改成一次发送多个消息包，并接受多个消息包。本质是合并消息包。
 * 
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Net;

namespace net_plugin.net
{
    /// <summary>
    /// Desc: 客户端Http
    /// Author: xiangjinbao
    /// </summary>
    public class HttpNet : NetObject
    {
        /// <summary>
        /// 发送消息线程
        /// </summary>
        private Thread mSendThread;
        /// <summary>
        /// Http锁
        /// </summary>
        private readonly object mHttpLock = new object();
        /// <summary>
        /// 消息开关
        /// </summary>
        private bool mHttpSwitch = true;
        /// <summary>
        /// 发送消息URL
        /// </summary>
        private string mURL;

        public HttpNet()
        {
            this.Init();
        }

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
        /// 设置Http消息
        /// </summary>
        /// <param name="url"></param>
        public void SetHttpURL(string url)
        {
            this.mURL = url;
        }

        /// <summary>
        /// 断开链接
        /// </summary>
        public override void DisConnect()
        {
            if (!string.IsNullOrEmpty(this.mURL))
            {
                this.mURL = string.Empty;
            }
            mSendThread = null;
        }

        /// <summary>
        /// 异步发送消息
        /// </summary>
        private void AsyncSend()
        {
            while (mHttpSwitch)
            {
                try
                {
                    if (mSendQueue.Count > 0)
                    {
                        lock (mSendLock)
                        {
                            WebClient http = new WebClient();
                            http.UploadDataCompleted += AsyncRevc;
                            http.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
                            byte[] packet = mSendQueue.Peek();
                            int packetId = BitConverter.ToInt32(packet, 0);
                            http.UploadData(this.mURL + packetId, packet);
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
        /// 异步接受返回消息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AsyncRevc(object sender, UploadDataCompletedEventArgs e)
        {
            if (null != e.Error)
            {
                throw new Exception("Revc Packet Error " + e.Error);
            }
            WebClient http = (WebClient)sender;
            // 处理返回消息包
            if (null != e.Result && e.Result.Length > 0)
            {
                byte[] packet = e.Result;
                lock (mRevcLock)
                {
                    mRevcQueue.Enqueue(packet);
                }
            }
        }

        /// <summary>
        /// Http是否有地址
        /// </summary>
        /// <returns></returns>
        public override bool Connected()
        {
            if (string.IsNullOrEmpty(this.mURL))
            {
                return false;
            }
            return true;
        }

    }
}
