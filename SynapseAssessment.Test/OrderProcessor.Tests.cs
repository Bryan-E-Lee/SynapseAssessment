using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Orders.Entities;
using Orders.Services;
using System.Collections.Concurrent;

namespace SynapseAssessment.Test
{
    /// <summary>
    /// We should test the order api as well, but given the expected time constraints we will avoid doing that for the
    /// purposes of completing this assessment.
    /// </summary>
    [TestClass]
    public class ProgramTests
    {
        //Compiler will not recognize the TestInitialize() attribute as initializing these properties
#pragma warning disable 8618
        private Mock<ILogger<BasicOrderProcessor>> mockLogger;
        private Mock<IOrderAlertService> mockApiService;
        private ConcurrentQueue<Order> retryQueue;
#pragma warning restore 8618

        [TestInitialize]
        public void BeforeTest()
        {
            mockLogger = new Mock<ILogger<BasicOrderProcessor>>();

            mockApiService = new Mock<IOrderAlertService>();
            mockApiService.Setup(mas => mas.SendAlertMessage(It.IsAny<Item>(), It.IsAny<string>()));
            mockApiService.Setup(mas => mas.SendAlertForUpdatedOrder(It.IsAny<Order>()));

            retryQueue = new();
        }

        [TestMethod]
        public async Task Process_NoOrders_Succeeds()
        {
            //Arrange
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act / Assert
            await orderProcessor.Process([]);
        }

        [TestMethod]
        public async Task Process_OneOrder_OneItem_WellFormed_Succeeds()
        {
            //Arrange
            var item = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var order = new Order
            {
                OrderId = "1",
                Items = [item]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            mockApiService.Verify(mas => mas.SendAlertMessage(item, order.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertForUpdatedOrder(order), Times.Once);
        }

        //Should add more tests to check for casing, but this will get to be many tests if we continue down that path for the purposes of this assessment.
        [TestMethod]
        public async Task Process_OneOrder_OneItem_IncrementsDelivery()
        {
            //Arrange
            var item = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var order = new Order
            {
                OrderId = "1",
                Items = [item]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            Assert.AreEqual(1, item.DeliveryNotification);
        }

        [TestMethod]
        public async Task Process_OneOrder_OneItemDelivered_DoesntIncrementDelivery()
        {
            //Arrange
            var item = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Not Delivered"
            };
            var order = new Order
            {
                OrderId = "1",
                Items = [item]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            Assert.AreEqual(0, item.DeliveryNotification);
        }

        [TestMethod]
        public async Task Process_OneOrder_MultipleItems_WellFormed_Succeeds()
        {
            //Arrange
            var item1 = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var item2 = new Item
            {
                Description = "Item 2",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var order = new Order
            {
                OrderId = "1",
                Items = [item1, item2]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            mockApiService.Verify(mas => mas.SendAlertMessage(item1, order.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertMessage(item2, order.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertForUpdatedOrder(order), Times.Once);
        }

        [TestMethod]
        public async Task Process_OneOrder_MultipleItems_IncrementsDeliveries()
        {
            //Arrange
            var item1 = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var item2 = new Item
            {
                Description = "Item 2",
                DeliveryNotification = 1,
                Status = "Delivered"
            };
            var order = new Order
            {
                OrderId = "1",
                Items = [item1, item2]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            Assert.AreEqual(1, item1.DeliveryNotification);
            Assert.AreEqual(2, item2.DeliveryNotification);
        }

        [TestMethod]
        public async Task Process_MultipleOrders_MultipleItems_Succeeds()
        {
            //Arrange
            var item1 = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var item2 = new Item
            {
                Description = "Item 2",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var item3 = new Item
            {
                Description = "Item 3",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var item4 = new Item
            {
                Description = "Item 4",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var order1 = new Order
            {
                OrderId = "1",
                Items = [item1, item2]
            };
            var order2 = new Order
            {
                OrderId = "2",
                Items = [item3, item4]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order1, order2]);

            //Assert
            mockApiService.Verify(mas => mas.SendAlertMessage(item1, order1.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertMessage(item2, order1.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertMessage(item3, order2.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertMessage(item4, order2.OrderId), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertForUpdatedOrder(order1), Times.Once);
            mockApiService.Verify(mas => mas.SendAlertForUpdatedOrder(order2), Times.Once);
        }

        [TestMethod]
        public async Task Process_OneOrder_MissingId_DoesNotNotify()
        {
            //Arrange
            var item = new Item
            {
                Description = "Item 1",
                DeliveryNotification = 0,
                Status = "Delivered"
            };
            var order = new Order
            {
                OrderId = string.Empty,
                Items = [item]
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            mockApiService.Verify(mas => mas.SendAlertMessage(item, order.OrderId), Times.Never);
            mockApiService.Verify(mas => mas.SendAlertForUpdatedOrder(order), Times.Never);
        }

        [TestMethod]
        public async Task Process_OneOrder_MissingId_QueuesForRetry()
        {
            //Arrange
            var order = new Order
            {
                OrderId = string.Empty
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order]);

            //Assert
            Assert.AreEqual(1, retryQueue.Count);
            Assert.IsTrue(retryQueue.TryPeek(out var queuedOrder));
            Assert.AreEqual(order, queuedOrder);
        }

        [TestMethod]
        public async Task Process_MultipleOrders_MissingIds_QueuesForRetry()
        {
            //Arrange
            var order1 = new Order
            {
                OrderId = string.Empty
            };
            var order2 = new Order
            {
                OrderId = string.Empty
            };
            var orderProcessor = new BasicOrderProcessor(mockApiService.Object, mockLogger.Object, retryQueue);

            //Act
            await orderProcessor.Process([order1, order2]);

            //Assert
            Assert.AreEqual(2, retryQueue.Count);
            CollectionAssert.Contains(retryQueue, order1);
            CollectionAssert.Contains(retryQueue, order2);
        }

        /**
         * There are several more tests we could write here and dividing these files up for readability, 
         * but for the purposes of simplicity for this assessment I will leave it as is.
         * */
    }
}
