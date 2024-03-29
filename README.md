# Fan-Light-as-a-Service

1. Connect a dimmable LED to a fan header on your system.

2. Figure out what the name of it is using [Fan Control](https://getfancontrol.com/) and set in `app.settings`

3. Run (requires administrator in order to control "fan speed")

4. Connect your service to [Home Assistant](https://www.home-assistant.io/)

```yaml

input_number:
  fanlight_brightness:
    initial: 191
    min: 0
    max: 255
    step: 1

compensation:
  fanlight_brightness:
    source: input_number.fanlight_brightness
    precision: 0
    data_points:
      - [0, 0]
      - [255, 100]

switch:
  - platform: rest
    resource: http://<YOUR_IP>:5112/
    body_on: '{"isOn": true, "brightness": {{ states("sensor.compensation_input_number_fanlight_brightness") }}}'
    body_off: '{"isOn": false}'
    is_on_template: "{{ value_json.isOn }}"
    headers:
      Content-Type: application/json

light:
  - platform: template
    lights:
      fanlight:
        value_template: "{{ states('switch.fanlight') == 'on' }}"
        turn_off:
          service: switch.turn_off
          target:
            entity_id: switch.fanlight
        turn_on:
          service: switch.turn_on
          target:
            entity_id: switch.fanlight
        set_level:
          - service: input_number.set_value
            data:
              value: "{{ brightness }}"
              entity_id: input_number.fanlight_brightness
          - service: switch.turn_on
            data_template:
              entity_id:
                - switch.fanlight

```
