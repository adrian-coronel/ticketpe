-- backend/scripts/seed.sql
INSERT INTO events (id, name, venue, starts_at)
VALUES ('11111111-1111-1111-1111-111111111111',
        'Concierto de prueba', 'Estadio Nacional', now() + interval '30 days');

INSERT INTO zones (event_id, name, price, currency)
VALUES ('11111111-1111-1111-1111-111111111111', 'VIP',      350.00, 'PEN'),
       ('11111111-1111-1111-1111-111111111111', 'Platea',   180.00, 'PEN'),
       ('11111111-1111-1111-1111-111111111111', 'General',   90.00, 'PEN');

-- 3 filas x 10 asientos por zona, generadas
INSERT INTO seats (event_id, zone, row, seat_number)
SELECT '11111111-1111-1111-1111-111111111111', z.name, r.row, s.num
FROM (VALUES ('VIP'), ('Platea'), ('General')) AS z(name),
     (VALUES ('A'), ('B'), ('C'))              AS r(row),
     generate_series(1, 10)                    AS s(num);