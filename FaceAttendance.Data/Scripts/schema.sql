-- Enable extension if needed (not strictly required for bytea but good practice)
-- CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    student_id VARCHAR(50) UNIQUE NOT NULL,
    embedding BYTEA,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS attendance (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id),
    date DATE NOT NULL,
    time TIME NOT NULL,
    status VARCHAR(20) DEFAULT 'Present',
    UNIQUE(user_id, date) -- Prevent duplicate attendance for same student on same day
);

CREATE TABLE IF NOT EXISTS admins (
    id SERIAL PRIMARY KEY,
    username VARCHAR(50) UNIQUE NOT NULL,
    password_hash VARCHAR(255) NOT NULL
);

-- Default admin password is 'admin123' (hashed using a simple plain text for demonstration, but typically you'd use bcrypt)
-- Using plain text here for simplicity, but in production you MUST hash it.
-- Let's assume we'll use a simple approach in the application or just plain text for now, but instructions say "change password".
-- Let's just insert a default user.
INSERT INTO admins (username, password_hash) VALUES ('admin', 'admin123') ON CONFLICT DO NOTHING;
