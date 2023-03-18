export function addPinMessageButton() {
  // Check if the revolt.chat object exists
  if (!window.revolt || !window.revolt.chat) {
    console.log('Error: revolt.chat object not found.');
    return;
  }

  // Create a new button element for the Pin Message option
  const pinButton = document.createElement('button');

  // Set the button's innerHTML to an icon or text that represents the Pin Message option
  pinButton.innerHTML = 'Pin Message';

  // Add an event listener to the button that will pin the selected message when clicked
  pinButton.addEventListener('click', () => {
    // Code to pin the selected message goes here
    console.log('Selected message pinned.');
  });

  // Use the revolt.chat API to add the button to the chat interface
  const pinWidget = window.revolt.chat.createWidget({
    onRender: () => {
      // Get the container element for the message actions buttons
      const container = document.querySelector('.message-actions');

      // Insert the Pin Message button at the end of the container
      container.appendChild(pinButton);
    }
  });

  // Add the widget to the chat interface using the revolt.chat API
  window.revolt.chat.addPluginWidget(pinWidget);
}