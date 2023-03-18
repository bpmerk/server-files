const Revolt = require('revolt.js');

// Initialize Revolt client with your bot token
const client = new Revolt.Client({
    token: 'YOUR_BOT_TOKEN'
});

// Event listener for when the bot connects to Revolt server
client.on('connected', () => {
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
            await client.channels.sendMessage(message.channel_id, 'You don\'t have permission to pin messages!');
            return;
        }

        // Get the ID of the message to pin
        const messageId = message.content.split(' ')[1];

        // Get the channel object
        const channel = await client.channels.fetch(message.channel_id);

        // Get the message object
        const messageToPin = await channel.messages.fetch(messageId);

        // Check if the message is already pinned
        if (messageToPin.isPinned) {
            await client.channels.sendMessage(message.channel_id, 'That message is already pinned!');
            return;
        }

        // Pin the message
        await messageToPin.pin();

        // Send a confirmation message
        await client.channels.sendMessage(message.channel_id, `Pinned message: ${messageToPin.content}`);
    }

    // Check if the message was sent in a channel where a message was just pinned
    if (message.channel.last_pin_timestamp && message.channel.last_pin_timestamp.getTime() === message.timestamp.getTime()) {
        // React to the pinned message with a pin emoji
        await message.react('📌');
    }
});

// Log in to the Revolt server with your bot token
client.login();
