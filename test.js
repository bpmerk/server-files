const { Client } = require('@revolt/api');

// Initialize Revolt client with your bot token
const client = new Client({
    token: 'aJen-eI-xU4Klyt7wfcqAb5lz2xg6ke6kAOx1Fl4TZICHRTNnDo6aYmJraDHtcz-'
});

// Event listener for when the bot connects to Revolt server
client.on('ready', () => {
    console.log(`Logged in as ${client.user.username}!`);
});

// Event listener for when a message is received
client.on('message', async (message) => {
    // Check if the message was sent by the bot itself
    if (message.author.bot) return;

    // Check if the message contains the command to pin a message
    if (message.content.startsWith('!pin')) {
        // Check if the user has permission to pin messages
        if (!message.member.permissions.has('pin_message')) {
            await message.channel.sendMessage('You don\'t have permission to pin messages!');
            return;
        }

        // Get the ID of the message to pin
        const messageId = message.content.split(' ')[1];

        // Get the message object
        const messageToPin = await client.channels.messages.fetch(message.channel_id, messageId);

        // Check if the message is already pinned
        if (messageToPin.pinned) {
            await message.channel.sendMessage('That message is already pinned!');
            return;
        }

        // Pin the message
        await messageToPin.pin();

        // Send a confirmation message
        await message.channel.sendMessage(`Pinned message: ${messageToPin.content}`);
    }

    // Check if the message was sent in a channel where a message was just pinned
    const lastPinnedMessage = await message.channel.fetchLastPinTimestamp();
    if (lastPinnedMessage && lastPinnedMessage.getTime() === message.timestamp.getTime()) {
        // React to the pinned message with a pin emoji
        await message.addReaction('📌');
    }
});

// Log in to the Revolt server with your bot token
client.connect();
