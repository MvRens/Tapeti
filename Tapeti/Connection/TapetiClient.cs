namespace Tapeti.Connection
{
    /*


        private async Task Cancel(TapetiChannel channel, string consumerTag, long connectionReference)
        {
            if (connection.IsClosing || string.IsNullOrEmpty(consumerTag))
                return;

            var capturedConnectionReference = connection.GetConnectionReference();

            // If the connection was re-established in the meantime, don't respond with an
            // invalid deliveryTag. The message will be requeued.
            if (capturedConnectionReference != connectionReference)
                return;

            // No need for a retryable channel here, if the connection is lost
            // so is the consumer.
            await channel.Queue(async innerChannel =>
            {
                // Check again as a reconnect may have occured in the meantime
                var currentConnectionReference = connection.GetConnectionReference();
                if (currentConnectionReference != connectionReference)
                    return;

                await innerChannel.BasicCancelAsync(consumerTag);
            }).ConfigureAwait(false);
        }






        /// <inheritdoc />
        public async Task Close()
        {
            // Empty the queue
            await defaultConsumeChannel.Close().ConfigureAwait(false);
            await defaultPublishChannel.Close().ConfigureAwait(false);

            foreach (var channel in dedicatedChannels)
                await channel.Close().ConfigureAwait(false);

            dedicatedChannels.Clear();
            await connection.Close();


            // Wait for message handlers to finish
            await messageHandlerTracker.WaitForIdle(CloseMessageHandlersTimeout);
        }

    }
    */
}
