const Revolt = require('revolt.js');
const axios = require('axios');

const client = new Revolt.Client();

client.login({
  email: '',
  password: ''
});

const gifUrl = 'https://api.giphy.com/v1/gifs/search';
const apiKey = '';

client.on('slashCommand', async (command) => {
  if (command.name === 'gif') {
    const query = command.arguments.join(' ');
    const { data } = await axios.get(gifUrl, {
      params: {
        api_key: apiKey,
        q: query,
        limit: 1,
        rating: 'g'
      }
    });
    const gif = data.data[0].images.original.url;
    const preview = data.data[0].images.fixed_height_small.url;
    const attachment = new Revolt.Attachment({
      url: gif,
      thumbnail_url: preview
    });
    await client.channels.sendMessage(command.channel_id, {
      content: `Here's a GIF for "${query}"`,
      attachments: [attachment]
    });
  }
});
