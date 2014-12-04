using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using Microsoft.Kinect;
using System.IO;

namespace DepthViewer
{
    public static class JsonHelper
    {
        [DataContract]
        public class JSONBlobsCollection 
        {
            [DataMember(Name = "blobs")]
            public Queue<JSONBlobs> Blobs { get; set; }
        }

        [DataContract]
        public class JSONBlobs 
        {
            [DataMember(Name = "id")]
            public int ID { get; set; }

            [DataMember(Name = "x")]
            public int X { get; set; }

            [DataMember(Name = "y")]
            public int Y { get; set; }

            [DataMember(Name = "width")]
            public int Width { get; set; }

            [DataMember(Name = "height")]
            public int Height { get; set; }
        }

        public static JSONBlobs BlobList(int id, int x, int y, int width, int height)
        {
            var data = new JSONBlobs { 
                ID = id,
                X = x,
                Y = y,
                Width = width,
                Height = height
            };

            return data;
        }

        public static string _Serialize(object obj)
        {
            DataContractJsonSerializer szer = new DataContractJsonSerializer(obj.GetType());
            MemoryStream stream = new MemoryStream();
            szer.WriteObject(stream, obj);
            string value = Encoding.Default.GetString(stream.ToArray());
            stream.Dispose();
            return value;
        }

        public static string SerializeBlob(this List<JSONBlobs> blobs)
        {
            JSONBlobsCollection blobsCollection = new JSONBlobsCollection
            {
                Blobs = new Queue<JSONBlobs>()
            };

            foreach (var b in blobs)
            {
                blobsCollection.Blobs.Enqueue(b);
            }
            return _Serialize(blobsCollection);
        }
    }
}
